static const float EPSILON = 1.401298E-45;
static const int VERTICES_PER_SPRITE = 4;

struct FontItem
{
    float4 Destination: Position;
    float4 Source: TEXCOORD0;
    float2 Origin: TEXCOORD1;
    float Depth : PSIZE0;
    float Rotation : PSIZE1;
    float4 Color: COLOR0;
    int SpriteEffect : BLENDINDICES0;
    // The atlas array slice this glyph was packed into - read by the pixel stage, see the note below.
    float Layer : PSIZE2;
    // A face the font lacks: x = the outline's outward move for a bold, in field units; y = an italic's slant; z = the
    // baseline's place in the quad (0 top, 1 bottom), which the slant pivots on.
    float4 Synthesis : TEXCOORD2;
    // A 'COLR' version 1 layer: x = its paint record plus one (0 = an ordinary glyph). Destination then holds the pen
    // and baseline (xy) and the pixels per font unit (zw).
    float4 Paint : TEXCOORD3;
};

// The atlas layer is a runtime index, so glyphs the packer spills to layers 1+ are sampled from their own layer.

struct PSInput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR;
    // The rounded ancestor clip's SHAPE, fetched from the transform table in the VERTEX stage like every other family
    // (see ClipFromSlot). That fetch is a SECOND read of the table from this shader, which used to kill the shader
    // compiler outright until the effect lost its two dead passes.
    nointerpolation float4 ClipBox : TEXCOORD1;
    nointerpolation float4 ClipRadii : TEXCOORD2;
    // The atlas array slice, as a runtime value - see the note above for why it was a compile-time 0 for so long.
    nointerpolation float Layer : TEXCOORD3;
    // How far a synthesized bold moves the glyph's edge out, in field units; 0 for a face drawn as it is.
    nointerpolation float Embolden : TEXCOORD4;
    // A 'COLR' version 1 layer's paint record plus one (0 = an ordinary glyph), the pixel's point in its gradient's
    // space, and the fade the layer's own colors take.
    nointerpolation float PaintRecord : TEXCOORD5;
    float2 PaintPoint : TEXCOORD6;
    nointerpolation float OwnFade : TEXCOORD7;
};

// A synthesized italic: the corner's shift right, for a quad of the given height, slanted about its baseline.
float SlantShift(float4 synthesis, float cornerY, float height)
{
    return (synthesis.z - cornerY) * height * synthesis.y;
}

Texture2DArray Texture : register(t1);
SamplerState TextureSampler : register(s1);

float4x4 MatrixTransform;
float2 TextureCornerCoords[4];
float4 ForegroundColor;
float FontSize;
float FontSizeThreshold;
float FontWeight;
float PxRange;
float2 MSDFAtlasSize;
// True-SDF blend band, in atlas texels per screen pixel. Below SdfBlendLo the glyph is magnified -> use the
// MSDF median (keeps sharp corners). Above SdfBlendHi it is minified -> use the single-channel true SDF
// (alpha), which stays crisp where median(bilinear) softens (its only cost, rounded corners, is sub-pixel
// at that size). Smoothly blended in between so there is no pop across the size threshold.
float SdfBlendLo;
float SdfBlendHi;
// The element's fade, raised to 2.2, for glyphs with a color of their own; ForegroundColor already carries it.
float GlyphFade;

// Rounded ancestor clip for the per-block draw: xy origin and zw size in device pixels (zw = 0: no clip), plus corner
// radii. One draw is one block with one clip; the batched pass reads the same values from the transform table.
float4 DirectClipBox;
float4 DirectClipRadii;

// Instanced glyph batch: per-instance GlyphData, and the transform table indexed by slot (0 = identity), so node-local
// glyphs move with one matrix write instead of a CPU re-bake.
uint64_t GlyphInstancesAddress;
uint64_t TransformsAddress;

// The layers of 'COLR' version 1 glyphs (Adamantium.Graphics/Fonts/ColorPaintStore.cs): a record per layer, in font
// units with y up, and the stops of its fill.
uint64_t PaintRecordsAddress;
uint64_t PaintStopsAddress;

struct PaintRecord
{
    float4 Quad;      // xy = the quad's top left corner, zw = its edge along u
    float4 AxisV;     // xy = its edge along v, zw = the corner's point in the gradient's space
    float4 Gradient;  // xy = the gradient-space step along u, zw = along v
    float4 Fill;      // x = kind (0 solid, 1 linear, 2 radial, 3 sweep), y = extend (0 pad, 1 repeat, 2 reflect),
                      // z = first stop, w = stop count
    float4 Shape;     // linear: start, end; radial: the two centers; sweep: center, start and end angle in radians
    float4 Extra;     // xy = a radial's two radii, z = the layer's opacity
};

struct PaintStop
{
    float4 Color;     // straight RGBA; a stop in the text's color keeps only its alpha, in a
    float4 Params;    // x = offset, y = 1 for the text's color
};

// One entry of the transform table (see Adamantium.UI/Rendering/TransformTable.cs). It carries the node's ALPHA beside
// its matrix - both are one node's state, and this shader must know the layout even though it only reads the matrix,
// or it would stride through the buffer wrong.
struct NodeSlot
{
    float4x4 World;
    float4   Params;   // .x = alpha (1 = opaque); .yzw reserved
};

// ---- Text clip: a rounded rectangle in device pixels, box.xy = origin, box.zw = size (0 = no clip), radii = TL, TR,
// BR, BL. The text's own code, so the engine does not depend on the UI's shader headers.

float TextClipDistance(float2 p, float2 halfSize, float4 radii)
{
    // SDF y is down, so a negative p.y is the top half.
    float r = p.x < 0.0 ? (p.y < 0.0 ? radii.x : radii.w)
                        : (p.y < 0.0 ? radii.y : radii.z);
    float2 q = abs(p) - halfSize + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, float2(0.0, 0.0))) - r;
}

float ClipCoverage(float2 fragment, float4 box, float4 radii)
{
    if (box.z <= 0.0) return 1.0;
    float2 halfSize = max(box.zw * 0.5, float2(1.0, 1.0));
    float2 local = fragment - (box.xy + halfSize);
    float lim = min(halfSize.x, halfSize.y);
    float d = TextClipDistance(local, halfSize, min(radii, float4(lim, lim, lim, lim)));
    float aa = fwidth(d) + 1e-4;
    return 1.0 - smoothstep(-aa, aa, d);
}

// The batch reads its clip from the transform table: row 0 of the slot's matrix is the box, row 1 the radii, and
// Params.x marks the slot as carrying one. A slot below 0 means no clip.
void ClipFromSlot(float slotIndex, out float4 box, out float4 radii)
{
    box = float4(0.0, 0.0, 0.0, 0.0);
    radii = float4(0.0, 0.0, 0.0, 0.0);
    if (slotIndex < 0.0) return;
    NodeSlot clip = ((NodeSlot*)TransformsAddress)[(uint)slotIndex];
    if (clip.Params.x < 0.5) return;
    box = clip.World[0];
    radii = clip.World[1];
}

// ---- 'COLR' version 1 layers: the quad is the record's parallelogram, placed at the pen and baseline and scaled to
// pixels; the fill is evaluated per pixel in the gradient's own space.

float2 PaintCorner(float paint, float2 corner, float4 origin, out float2 gradientPoint)
{
    PaintRecord record = ((PaintRecord*)PaintRecordsAddress)[(uint)(paint - 1.0)];
    float2 position = record.Quad.xy + corner.x * record.Quad.zw + corner.y * record.AxisV.xy;
    gradientPoint = record.AxisV.zw + corner.x * record.Gradient.xy + corner.y * record.Gradient.zw;
    return origin.xy + float2(position.x, -position.y) * origin.zw;
}

float4 StopColor(PaintStop stop, float4 foreground)
{
    return stop.Params.y > 0.5 ? float4(foreground.rgb, stop.Color.a) : stop.Color;
}

// What the stop's alpha is multiplied by after it is raised to 2.2: the text color's alpha for a stop in the text's
// color, the fade for one of the font's own; both already stand raised, as the pixel shader's gamma boost expects.
float StopFade(PaintStop stop, float4 foreground, float ownFade)
{
    return stop.Params.y > 0.5 ? foreground.a : ownFade;
}

float ExtendPosition(float t, float extend)
{
    if (extend > 1.5) return 1.0 - abs(frac(t * 0.5) * 2.0 - 1.0);
    if (extend > 0.5) return frac(t);
    return saturate(t);
}

// The color line at t, in the stops' own offsets: extended past its first and last stop, interpolated premultiplied,
// its alpha linear; the stops' fades come out interpolated beside it.
float4 ColorLine(PaintRecord record, float t, float4 foreground, float ownFade, out float fade)
{
    PaintStop* stops = ((PaintStop*)PaintStopsAddress) + (uint)record.Fill.z;
    uint count = (uint)record.Fill.w;
    fade = 0.0;
    if (count == 0) return float4(0.0, 0.0, 0.0, 0.0);
    float first = stops[0].Params.x;
    float last = stops[count - 1].Params.x;
    if (last > first)
    {
        t = first + ExtendPosition((t - first) / (last - first), record.Fill.y) * (last - first);
    }

    float4 previous = StopColor(stops[0], foreground);
    fade = StopFade(stops[0], foreground, ownFade);
    if (t <= first) return previous;
    for (uint i = 1; i < count; i++)
    {
        float4 next = StopColor(stops[i], foreground);
        float nextFade = StopFade(stops[i], foreground, ownFade);
        float from = stops[i - 1].Params.x;
        float to = stops[i].Params.x;
        if (t <= to)
        {
            float k = to > from ? (t - from) / (to - from) : 1.0;
            fade = lerp(fade, nextFade, k);
            float4 mixed = lerp(float4(previous.rgb * previous.a, previous.a), float4(next.rgb * next.a, next.a), k);
            return mixed.a > 0.0 ? float4(mixed.rgb / mixed.a, mixed.a) : float4(0.0, 0.0, 0.0, 0.0);
        }

        previous = next;
        fade = nextFade;
    }

    return previous;
}

// A two-point conical gradient's t at a point: the largest t whose circle passes through it with a radius of 0 or more;
// false where no circle does.
bool RadialPosition(PaintRecord record, float2 p, out float t)
{
    float2 c0 = record.Shape.xy;
    float r0 = record.Extra.x;
    float2 cd = record.Shape.zw - c0;
    float2 pd = p - c0;
    float dr = record.Extra.y - r0;
    float a = dot(cd, cd) - dr * dr;
    float b = dot(pd, cd) + r0 * dr;
    float c = dot(pd, pd) - r0 * r0;
    t = 0.0;
    if (abs(a) < 1e-6)
    {
        if (abs(b) < 1e-6) return false;
        t = c / (2.0 * b);
        return r0 + t * dr >= 0.0;
    }

    float discriminant = b * b - a * c;
    if (discriminant < 0.0) return false;
    float root = sqrt(discriminant);
    float t1 = (b + root) / a;
    float t2 = (b - root) / a;
    t = max(t1, t2);
    if (r0 + t * dr >= 0.0) return true;
    t = min(t1, t2);
    return r0 + t * dr >= 0.0;
}

float4 PaintColor(float paint, float2 p, float4 foreground, float ownFade)
{
    PaintRecord record = ((PaintRecord*)PaintRecordsAddress)[(uint)(paint - 1.0)];
    float kind = record.Fill.x;
    float t = 0.0;
    if (kind > 2.5)
    {
        float2 d = p - record.Shape.xy;
        float angle = atan2(d.y, d.x);
        if (angle < 0.0) angle += 6.28318530718;
        float span = record.Shape.w - record.Shape.z;
        // An empty sweep keeps only its two end colors, padded; repeated or reflected it has nothing to repeat.
        if (abs(span) <= 1e-6 && record.Fill.y > 0.5) return float4(0.0, 0.0, 0.0, 0.0);
        t = abs(span) > 1e-6 ? (angle - record.Shape.z) / span : (angle < record.Shape.z ? -1e6 : 1e6);
    }
    else if (kind > 1.5)
    {
        if (!RadialPosition(record, p, t)) return float4(0.0, 0.0, 0.0, 0.0);
    }
    else if (kind > 0.5)
    {
        float2 d = record.Shape.zw - record.Shape.xy;
        float length2 = dot(d, d);
        if (length2 <= 0.0) return float4(0.0, 0.0, 0.0, 0.0);
        t = dot(p - record.Shape.xy, d) / length2;
    }

    // The pixel shader's gamma boost is for coverage: a gradient's alpha goes in raised to 2.2 so it comes out linear,
    // or a translucent shading layer comes out as good as twice as dark.
    float fade;
    float4 color = ColorLine(record, t, foreground, ownFade, fade);
    color.a = pow(color.a * record.Extra.z, 2.2) * fade;
    return color;
}

// Per-glyph quad expansion, now in the VERTEX stage (corner from SV_VertexID), so the geometry shader is gone:
// plain instanced rendering (4-vertex triangle strip x N glyphs), portable to Metal/MoltenVK.
PSInput ExpandGlyphCorner(FontItem item, int corner)
{
    PSInput vertex;
    float2 origin = item.Origin;
    float2 rotation = float2(cos(item.Rotation), sin(item.Rotation));

    float2 cornerCoord = TextureCornerCoords[corner];
    float2 size = cornerCoord * item.Destination.zw;
    size.x += SlantShift(item.Synthesis, cornerCoord.y, item.Destination.w);
    float2 position = size - origin;

    [flatten]
    if (item.Rotation != 0.0)
    {
        vertex.Position.x = item.Destination.x + (position.x * rotation.x) - (position.y * rotation.y);
        vertex.Position.y = item.Destination.y + (position.x * rotation.y) + (position.y * rotation.x);
        vertex.Position.xy += origin;
    }
    else
    {
        vertex.Position.xy = item.Destination.xy + size;
    }

    float2 paintPoint = float2(0.0, 0.0);
    if (item.Paint.x > 0.5)
    {
        vertex.Position.xy = PaintCorner(item.Paint.x, cornerCoord, item.Destination, paintPoint);
    }

    vertex.PaintRecord = item.Paint.x;
    vertex.PaintPoint = paintPoint;
    vertex.OwnFade = GlyphFade;

    vertex.Position.z = item.Depth;
    vertex.Position.w = 1;
    vertex.Color = item.Color;

    float2 uvCorner = TextureCornerCoords[corner ^ item.SpriteEffect];
    vertex.UV = item.Source.xy + uvCorner * item.Source.zw;

    // The clip comes in as a uniform here (see DirectClipBox) instead of from the table by slot, and it has to be
    // WRITTEN either way: a varying this vertex shader does not set reaches the pixel shader as whatever was in the
    // register, and the batch shares this PSInput with it. A zero box is "no clip".
    vertex.Layer = item.Layer;
    vertex.Embolden = item.Synthesis.x;
    vertex.ClipBox = DirectClipBox;
    vertex.ClipRadii = DirectClipRadii;

    vertex.Position = mul(vertex.Position, MatrixTransform);
    return vertex;
}

float Median(float r, float g, float b)
{
    return max(min(r, g), min(max(r, g), b));
}

float ScreenPxRange(float2 uv)
{
    float2 unitRange = float2(PxRange, PxRange) / MSDFAtlasSize;
    float2 screenTexSize = float2(1.0, 1.0) / fwidth(uv);
    return max(0.5 * dot(unitRange, screenTexSize), 1.0);
}

// Coverage source for the glyph body: MSDF median when magnified (sharp corners), true-SDF alpha when
// minified (crisp where median(bilinear) softens), blended by the minification factor = the max UV
// derivative in atlas texels (the standard texture-LOD metric). With SdfBlendLo >= SdfBlendHi (or both very
// large) it stays pure MSDF, so the blend can be disabled purely via the uniforms - no hardcoded switch.
float SampleGlyphCoverage(float4 samp, float2 uv)
{
    float msdf = Median(samp.r, samp.g, samp.b);
    float texelsPerPx = max(length(ddx(uv) * MSDFAtlasSize), length(ddy(uv) * MSDFAtlasSize));
    float t = smoothstep(SdfBlendLo, SdfBlendHi, texelsPerPx);
    return lerp(msdf, samp.a, t);
}

[shader("vertex")]
PSInput FontVertexShader(FontItem item, uint vertexId : SV_VertexID)
{
    return ExpandGlyphCorner(item, (int)vertexId);   // vertexId 0..3 = strip corner
}

// Canonical MSDF reconstruction (Chlumsky). ScreenPxRange() gives the field slope in screen pixels.
// FontWeight shifts the 0.5 contour INSIDE the ScreenPxRange term (a true distance bias, not an opacity
// add), so it makes stems thinner/thicker without hazing the background. Selected via the RenderMsdf pass,
// toggled from FontRenderer.UseCanonicalMsdf.
[shader("fragment")]
float4 FontPixelShaderMsdf(PSInput input) : SV_Target
{
    float4 samp = Texture.Sample(TextureSampler, float3(input.UV, input.Layer));
    float sd = SampleGlyphCoverage(samp, input.UV);
    float opacity = clamp(ScreenPxRange(input.UV) * (sd - 0.5 + FontWeight + input.Embolden) + 0.5, 0.0, 1.0);
    // A glyph of attributed text brings its own color; a negative alpha means the element's foreground.
    float4 color = input.Color.a < 0 ? ForegroundColor : float4(input.Color.rgb, input.Color.a * GlyphFade);
    if (input.PaintRecord > 0.5)
    {
        color = PaintColor(input.PaintRecord, input.PaintPoint, color, input.OwnFade);
    }
    // Gamma-boost coverage times the color's alpha so thin stems keep their color; splitting the two washed text out.
    // The element's fade arrives pre-raised to 2.2, so the boost hands it back linear.
    float alpha = pow(color.a * opacity, 1.0 / 2.2);
    // The rounded ancestor clip, as coverage, exactly as the batch pass applies it. Both the premultiplied color and
    // the alpha are cut: this pass outputs rgb*alpha, so cutting one without the other leaves color where the glyph
    // was cut away. A zero-size box gives 1 and costs nothing.
    alpha *= ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return float4(color.rgb * alpha, alpha);
}

// Batch variant of FontPixelShaderMsdf: the color comes per instance (input.Color), so one instanced draw renders glyphs
// of many text blocks, each in its own color.
[shader("fragment")]
float4 FontPixelShaderMsdfBatch(PSInput input) : SV_Target
{
    float4 samp = Texture.Sample(TextureSampler, float3(input.UV, input.Layer));
    float sd = SampleGlyphCoverage(samp, input.UV);
    float opacity = clamp(ScreenPxRange(input.UV) * (sd - 0.5 + FontWeight + input.Embolden) + 0.5, 0.0, 1.0);
    // Unchanged on purpose - the element's fade is pre-compensated in the vertex stage so that this very boost hands
    // it back linear. See the FADE line in FontBatchInstancedVS.
    float4 color = input.Color;
    if (input.PaintRecord > 0.5)
    {
        color = PaintColor(input.PaintRecord, input.PaintPoint, color, input.OwnFade);
    }

    float alpha = pow(color.a * opacity, 1.0 / 2.2);
    // The rounded ancestor clip, as coverage. Applied to the PREMULTIPLIED color as well as the alpha - this pass
    // outputs rgb*alpha, so cutting only the alpha would leave the color standing where the glyph was cut away.
    alpha *= ClipCoverage(input.Position.xy, input.ClipBox, input.ClipRadii);
    return float4(color.rgb * alpha, alpha);
}

// ---- Instanced glyph batch: per-instance GlyphData read from a BDA STORAGE buffer by SV_InstanceID (mirrors
// RectBatchInstancedVS in BatchEffect.fx); the quad comes from SV_VertexID. Node-local glyph rects are transformed to
// world on the GPU by the instance's transform-table slot (0 = identity), so a scrolling block moves via one matrix
// write, not a per-glyph CPU re-bake, and the batch is node-aware. Reuses FontPixelShaderMsdfBatch (per-instance color).
struct GlyphData
{
    float4 LocalRect;   // node-local x, y, w, h (world for slot-0 legacy bakes)
    float4 Source;      // atlas UV rect
    float4 Params;      // .x = transform-table slot; .y = atlas layer (read by the PS); .z = depth; .w reserved
    float4 Clip;        // .x = the ROUNDED CLIP's slot, or -1; .yzw = the glyph's synthesis (FontItem.Synthesis.xyz)
    float4 Color;       // straight RGBA, element/brush opacity folded into .w
    float4 Paint;       // .x = a 'COLR' version 1 layer's paint record plus one, or 0 (LocalRect is then the pen and
                        // baseline, xy, and pixels per font unit, zw); .y = the element's opacity raised to 2.2, for the
                        // layer's own colors
};

[shader("vertex")]
PSInput FontBatchInstancedVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    GlyphData* items = (GlyphData*)GlyphInstancesAddress;
    GlyphData g = items[instanceId];

    PSInput o;
    // SAME corner mapping as ExpandGlyphCorner (TextureCornerCoords[vertexId]) so the quad + UV match the direct path.
    float2 corner = TextureCornerCoords[vertexId];
    float2 localPos = g.LocalRect.xy + corner * g.LocalRect.zw;
    localPos.x += SlantShift(float4(0.0, g.Clip.z, g.Clip.w, 0.0), corner.y, g.LocalRect.w);
    float2 paintPoint = float2(0.0, 0.0);
    if (g.Paint.x > 0.5)
    {
        localPos = PaintCorner(g.Paint.x, corner, g.LocalRect, paintPoint);
    }
    // Node-local -> world via the instance's transform-table matrix (slot 0 = identity for legacy world bakes).
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    float4x4 nodeWorld = nodes[(uint)g.Params.x].World;
    float4 worldPos = mul(float4(localPos, g.Params.z, 1.0), nodeWorld);
    o.Position = mul(worldPos, MatrixTransform);   // MatrixTransform = the (transposed-on-upload) projection
    o.UV = g.Source.xy + corner * g.Source.zw;     // SpriteEffect == 0 for batched glyphs
    // The element's alpha from the OPACITY SLOT, exactly as every other family reads it: a fading ancestor then moves
    // one number in the table instead of re-baking every glyph under it. Params.w is -1 when nothing above fades.
    float fadeSlot = g.Params.w;
    float fade = nodes[(uint)max(fadeSlot, 0.0)].Params.x;
    fade = lerp(1.0, fade, step(0.0, fadeSlot));
    // The fade is raised to 2.2 so the pixel shader's gamma boost hands back exactly `fade`, matching the shapes beside it.
    o.Color = float4(g.Color.rgb, g.Color.a * pow(fade, 2.2));
    o.PaintRecord = g.Paint.x;
    o.PaintPoint = paintPoint;
    o.OwnFade = g.Paint.y * pow(fade, 2.2);
    // The clip's shape, from the table by the slot the record carries - one fetch per instance, as everywhere else.
    o.Layer = g.Params.y;   // the atlas layer this glyph was packed into
    o.Embolden = g.Clip.y;
    ClipFromSlot(g.Clip.x, o.ClipBox, o.ClipRadii);
    return o;
}

technique FontBatch
{
    pass RenderMsdf
    {
        EffectName = "FontEffectMsdf";
        VertexShader = FontVertexShader;
        PixelShader = FontPixelShaderMsdf;
    }

    pass RenderMsdfBatchInstanced
    {
        EffectName = "FontEffectMsdfBatchInstanced";
        VertexShader = FontBatchInstancedVS;
        PixelShader = FontPixelShaderMsdfBatch;
    }
}
