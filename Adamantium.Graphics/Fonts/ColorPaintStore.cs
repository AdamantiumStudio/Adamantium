using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics.Fonts;

internal sealed class ColorPaintStore : IDisposable
{
    private const int RecordSize = 6;
    private const int StopSize = 2;
    private const int InitialCapacity = 256;

    private readonly object _gate = new();
    private readonly Dictionary<(ulong Glyph, int Layer), int> _records = new();
    private readonly List<Vector4F> _recordData = [];
    private readonly List<Vector4F> _stopData = [];
    private Buffer<Vector4F> _recordBuffer;
    private Buffer<Vector4F> _stopBuffer;
    private int _recordsUploaded;
    private int _stopsUploaded;
    private IGraphicsDevice _device;
    private bool _disposed;

    public int GetRecord(ulong colorGlyph, int layerIndex, ColorPaintLayer layer, Glyph mask, double leftSideBearing,
        GlyphTextureData cell, double unitsPerEm, uint fieldSize)
    {
        lock (_gate)
        {
            if (_records.TryGetValue((colorGlyph, layerIndex), out var known))
            {
                return known;
            }

            var record = Build(layer, mask, leftSideBearing, cell, unitsPerEm, fieldSize);
            _records[(colorGlyph, layerIndex)] = record;
            return record;
        }
    }

    public (ulong Records, ulong Stops) Upload(IGraphicsDevice device)
    {
        lock (_gate)
        {
            if (_disposed || _recordData.Count == 0)
            {
                return (0, 0);
            }

            _device = device;
            _recordsUploaded = Send(device, ref _recordBuffer, _recordData, _recordsUploaded);
            _stopsUploaded = Send(device, ref _stopBuffer, _stopData, _stopsUploaded);
            return (_recordBuffer.GetDeviceAddress(), _stopBuffer.GetDeviceAddress());
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            Retire(_recordBuffer);
            Retire(_stopBuffer);
            _recordBuffer = null;
            _stopBuffer = null;
        }
    }

    private int Build(ColorPaintLayer layer, Glyph mask, double leftSideBearing, GlyphTextureData cell,
        double unitsPerEm, uint fieldSize)
    {
        var fill = layer.Fill;
        var toGradient = fill.Transform;
        if (fill.Stops.Count == 0 || Math.Abs(toGradient.Determinant()) < 1e-12 ||
            Math.Abs(layer.Transform.Determinant()) < 1e-12)
        {
            return -1;
        }

        toGradient = Matrix3x2.Invert(toGradient);

        var bounds = mask.BoundingRectangle;
        var margin = cell.Margin - 0.5;
        var uniform = margin * unitsPerEm / fieldSize;
        var marginX = bounds.Width > 0 && cell.BoundingRect.Width > 0 ? margin * bounds.Width / cell.BoundingRect.Width : uniform;
        var marginY = bounds.Height > 0 && cell.BoundingRect.Height > 0 ? margin * bounds.Height / cell.BoundingRect.Height : uniform;

        var corner = new Vector2(leftSideBearing - marginX, bounds.Y + bounds.Height + marginY);
        var origin = Matrix3x2.TransformPoint(layer.Transform, corner);
        var axisU = Linear(layer.Transform, new Vector2(bounds.Width + 2 * marginX, 0));
        var axisV = Linear(layer.Transform, new Vector2(0, -(bounds.Height + 2 * marginY)));
        var gradientOrigin = Matrix3x2.TransformPoint(toGradient, origin);
        var gradientU = Linear(toGradient, axisU);
        var gradientV = Linear(toGradient, axisV);

        var record = _recordData.Count / RecordSize;
        var firstStop = _stopData.Count / StopSize;
        foreach (var stop in fill.Stops)
        {
            if (stop.Color is { } color)
            {
                _stopData.Add(new Vector4F(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f));
                _stopData.Add(new Vector4F(stop.Offset, 0, 0, 0));
            }
            else
            {
                _stopData.Add(new Vector4F(0, 0, 0, stop.Alpha));
                _stopData.Add(new Vector4F(stop.Offset, 1, 0, 0));
            }
        }

        _recordData.Add(new Vector4F((float)origin.X, (float)origin.Y, (float)axisU.X, (float)axisU.Y));
        _recordData.Add(new Vector4F((float)axisV.X, (float)axisV.Y, (float)gradientOrigin.X, (float)gradientOrigin.Y));
        _recordData.Add(new Vector4F((float)gradientU.X, (float)gradientU.Y, (float)gradientV.X, (float)gradientV.Y));
        _recordData.Add(new Vector4F((float)fill.Kind, (float)fill.Extend, firstStop, fill.Stops.Count));
        _recordData.Add(Shape(fill));
        _recordData.Add(new Vector4F((float)fill.Radius0, (float)fill.Radius1, layer.Opacity, 0));
        return record;
    }

    private static Vector4F Shape(ColorFill fill)
    {
        switch (fill.Kind)
        {
            case ColorFillKind.LinearGradient:
                var end = LinearEnd(fill.Point0, fill.Point1, fill.Point2);
                return new Vector4F((float)fill.Point0.X, (float)fill.Point0.Y, (float)end.X, (float)end.Y);
            case ColorFillKind.SweepGradient:
                return new Vector4F((float)fill.Point0.X, (float)fill.Point0.Y, (float)(fill.StartAngle * Math.PI / 180),
                    (float)(fill.EndAngle * Math.PI / 180));
            default:
                return new Vector4F((float)fill.Point0.X, (float)fill.Point0.Y, (float)fill.Point1.X, (float)fill.Point1.Y);
        }
    }

    private static Vector2 LinearEnd(Vector2 p0, Vector2 p1, Vector2 p2)
    {
        var normal = new Vector2(p2.Y - p0.Y, -(p2.X - p0.X));
        var length = normal.X * normal.X + normal.Y * normal.Y;
        if (length < 1e-12)
        {
            return p1;
        }

        var along = ((p1.X - p0.X) * normal.X + (p1.Y - p0.Y) * normal.Y) / length;
        return new Vector2(p0.X + normal.X * along, p0.Y + normal.Y * along);
    }

    private static Vector2 Linear(Matrix3x2 m, Vector2 v) =>
        new(v.X * m.M11 + v.Y * m.M21, v.X * m.M12 + v.Y * m.M22);

    private int Send(IGraphicsDevice device, ref Buffer<Vector4F> buffer, List<Vector4F> data, int uploaded)
    {
        if (buffer == null || (int)buffer.ElementCount < data.Count)
        {
            Retire(buffer);
            var capacity = Math.Max(InitialCapacity, (int)(buffer?.ElementCount ?? 0) * 2);
            while (capacity < data.Count)
            {
                capacity *= 2;
            }

            buffer = Buffer.New<Vector4F>(device, (ulong)capacity,
                BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                BufferMemoryUsage.UploadFromCpuToGpu);
            uploaded = 0;
        }

        if (uploaded < data.Count)
        {
            var span = CollectionsMarshal.AsSpan(data).Slice(uploaded);
            buffer.SetData((ReadOnlySpan<Vector4F>)span, (uint)(uploaded * Marshal.SizeOf<Vector4F>()));
        }

        return data.Count;
    }

    private void Retire(Buffer<Vector4F> buffer)
    {
        if (buffer != null)
        {
            _device.AddToDeferDisposeQueue(buffer);
        }
    }
}
