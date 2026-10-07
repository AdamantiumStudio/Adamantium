using System;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics;

/// <summary>
/// A 2D texture array that grows as its layers are taken, the way <see cref="System.Collections.Generic.List{T}"/>
/// grows: <see cref="Count"/> layers are in use and <see cref="Capacity"/> are allocated, and taking one past the
/// capacity replaces the texture with one twice as deep, the layers in use copied over on the GPU. The texture it
/// replaced is disposed once no frame in flight can read it, so read <see cref="Texture"/> each time it is bound.
/// </summary>
public sealed class GrowingTextureArray : DisposableObject
{
    private readonly IGraphicsDevice _device;
    private readonly string _name;
    private TextureDescription _description;

    /// <summary>A texture array of <paramref name="description"/>, its <c>ArrayLayers</c> the starting capacity, that
    /// grows up to <paramref name="maxCapacity"/> layers or the device's limit, whichever is smaller.</summary>
    public GrowingTextureArray(IGraphicsDevice device, TextureDescription description, uint maxCapacity = uint.MaxValue,
        string name = "")
    {
        _device = device;
        _name = name;
        _description = description;
        _description.ArrayLayers = Math.Max(1, description.ArrayLayers);
        var deviceLimit = device.Adapter.AdapterProperties.Limits.MaxImageArrayLayers;
        MaxCapacity = Math.Max(_description.ArrayLayers, Math.Min(maxCapacity, deviceLimit));
        Texture = Texture.New(device, _description, name);
    }

    /// <summary>The texture as it stands; a new one after each growth.</summary>
    public Texture Texture { get; private set; }

    /// <summary>The layers taken.</summary>
    public uint Count { get; private set; }

    /// <summary>The layers allocated.</summary>
    public uint Capacity => _description.ArrayLayers;

    /// <summary>The most layers the array may grow to.</summary>
    public uint MaxCapacity { get; }

    /// <summary>Raised after <see cref="Texture"/> has been replaced by a deeper one.</summary>
    public event EventHandler Grown;

    /// <summary>Takes the next layer and gives its index, growing the array when every layer is taken. False when the
    /// array is at <see cref="MaxCapacity"/> and full.</summary>
    public bool TryAdd(out uint layer)
    {
        layer = Count;
        if (Count == Capacity)
        {
            if (Capacity == MaxCapacity)
            {
                return false;
            }

            EnsureCapacity(Count + 1);
        }

        Count++;
        return true;
    }

    /// <summary>Grows the array to at least <paramref name="layers"/> layers, doubling as a list does.</summary>
    public void EnsureCapacity(uint layers)
    {
        if (layers <= Capacity)
        {
            return;
        }

        if (layers > MaxCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(layers), layers,
                $"The array may grow to {MaxCapacity} layers.");
        }

        var capacity = (uint)Math.Min(MaxCapacity, Math.Max(layers, (ulong)Capacity * 2));
        var description = _description;
        description.ArrayLayers = capacity;
        var grown = Texture.New(_device, description, _name);
        if (Count > 0)
        {
            CopyLayers(Texture, grown, Count);
        }

        _device.AddToDeferDisposeQueue(Texture);
        Texture = grown;
        _description = description;
        Grown?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposeManagedResources)
    {
        if (disposeManagedResources)
        {
            Texture?.Dispose();
            Texture = null;
        }

        base.Dispose(disposeManagedResources);
    }

    private void CopyLayers(Texture source, Texture destination, uint layers)
    {
        var sourceLayout = source.ImageLayout;
        source.TransitionImageLayout(ImageLayout.TransferSrcOptimal);
        destination.TransitionImageLayout(ImageLayout.TransferDstOptimal);

        var commandBuffer = _device.BeginSingleTimeCommand();
        var copy = new ImageCopy
        {
            SrcSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlagBits.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = layers,
            },
            DstSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlagBits.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = layers,
            },
            SrcOffset = new Offset3D(),
            DstOffset = new Offset3D(),
            Extent = new Extent3D { Width = source.Width, Height = source.Height, Depth = 1 },
        };
        commandBuffer.CopyImage(source.GetImage(), ImageLayout.TransferSrcOptimal, destination.GetImage(),
            ImageLayout.TransferDstOptimal, 1, copy);
        _device.EndSingleTimeCommand(commandBuffer);

        if (sourceLayout is not (ImageLayout.Undefined or ImageLayout.Preinitialized))
        {
            source.TransitionImageLayout(sourceLayout);
        }

        destination.TransitionImageLayout(_description.DesiredImageLayout);
    }
}
