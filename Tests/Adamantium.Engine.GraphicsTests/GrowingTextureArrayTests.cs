using System.Runtime.InteropServices;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Imaging;
using Adamantium.Vulkan.Core;
using NUnit.Framework;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.Engine.GraphicsTests;

[TestFixture]
public class GrowingTextureArrayTests
{
    private const uint Size = 16;

    [TearDown]
    public void ReleaseDevices() => GpuFixture.ReleaseRenderDevices();

    private static GrowingTextureArray NewArray(IGraphicsDevice device, uint layers, uint maxCapacity = uint.MaxValue)
    {
        var description = new TextureDescription
        {
            Width = Size,
            Height = Size,
            Depth = 1,
            ArrayLayers = layers,
            MipLevels = 1,
            Samples = MSAALevel.None,
            Format = Format.R8G8B8A8_UNORM,
            InitialLayout = ImageLayout.Preinitialized,
            DesiredImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageType = ImageType._2d,
            ImageAspect = ImageAspectFlagBits.ColorBit,
            Usage = ImageUsageFlagBits.SampledBit | ImageUsageFlagBits.TransferDstBit | ImageUsageFlagBits.TransferSrcBit,
            Dimension = TextureDimension.Texture2D,
        };
        return new GrowingTextureArray(device, description, maxCapacity);
    }

    private static void Fill(IGraphicsDevice device, Texture texture, uint layer, byte r, byte g, byte b)
    {
        var pixels = new byte[Size * Size * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }

        using var buffer = Buffer.New(device, pixels, BufferUsageFlags.TransferSrc,
            MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);
        texture.TransitionImageLayout(ImageLayout.TransferDstOptimal);
        var commandBuffer = device.BeginSingleTimeCommand();
        var region = new BufferImageCopy
        {
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlagBits.ColorBit,
                BaseArrayLayer = layer,
                LayerCount = 1,
            },
            ImageExtent = new Extent3D { Width = Size, Height = Size, Depth = 1 },
        };
        commandBuffer.CopyBufferToImage(buffer, texture, ImageLayout.TransferDstOptimal, 1, region);
        device.EndSingleTimeCommand(commandBuffer);
        texture.TransitionImageLayout(ImageLayout.ShaderReadOnlyOptimal);
    }

    private static byte[] FirstPixel(Texture texture, uint layer)
    {
        using var image = texture.ReadbackToImage(layer);
        var pixel = new byte[4];
        Marshal.Copy(image.DataPointer, pixel, 0, 4);
        return pixel;
    }

    [Test]
    public void TakingALayerPastTheCapacity_DoublesIt()
    {
        var device = GpuFixture.CreateRenderDevice();
        using var array = NewArray(device, 2);
        var grown = 0;
        array.Grown += (_, _) => grown++;
        var first = array.Texture;

        for (var i = 0; i < 3; i++)
        {
            Assert.That(array.TryAdd(out var layer), Is.True);
            Assert.That(layer, Is.EqualTo((uint)i));
        }

        Assert.That(array.Count, Is.EqualTo(3));
        Assert.That(array.Capacity, Is.EqualTo(4));
        Assert.That(grown, Is.EqualTo(1));
        Assert.That(array.Texture, Is.Not.SameAs(first), "a deeper texture replaces the full one");
    }

    [Test]
    public void TheLayersInUse_SurviveTheGrowth()
    {
        var device = GpuFixture.CreateRenderDevice();
        using var array = NewArray(device, 2);
        array.TryAdd(out _);
        array.TryAdd(out _);
        Fill(device, array.Texture, 0, 255, 0, 0);
        Fill(device, array.Texture, 1, 0, 255, 0);

        array.TryAdd(out _);

        Assert.That(FirstPixel(array.Texture, 0), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
        Assert.That(FirstPixel(array.Texture, 1), Is.EqualTo(new byte[] { 0, 255, 0, 255 }));
    }

    [Test]
    public void AtItsLimit_TheArrayStopsTaking()
    {
        var device = GpuFixture.CreateRenderDevice();
        using var array = NewArray(device, 2, maxCapacity: 3);

        Assert.That(array.TryAdd(out _) && array.TryAdd(out _) && array.TryAdd(out _), Is.True);
        Assert.That(array.TryAdd(out _), Is.False);
        Assert.That(array.Capacity, Is.EqualTo(3), "doubling stops at the limit");
    }
}
