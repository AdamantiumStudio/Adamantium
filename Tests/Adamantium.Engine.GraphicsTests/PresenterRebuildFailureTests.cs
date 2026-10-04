using System;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using NUnit.Framework;

namespace Adamantium.Engine.GraphicsTests;

/// <summary>A presenter whose rebuild failed has no surfaces; the device must skip the frame, not draw into nothing.</summary>
[TestFixture]
public class PresenterRebuildFailureTests
{
    private const uint Size = 64;

    private sealed class FailedRebuildPresenter(IGraphicsDevice device, PresentationParameters parameters)
        : RenderTargetGraphicsPresenter(device, parameters, "FailedRebuild")
    {
        public void LoseSurfaces()
        {
            IsReady = false;
            DisposeFrameSurfaces();
        }
    }

    [TearDown]
    public void ReleaseDevices() => GpuFixture.ReleaseRenderDevices();

    private static bool BeginFrame(IGraphicsDevice device, GraphicsPresenter presenter)
    {
        device.SetRenderTargets(presenter.RenderTarget);
        device.SetDepthBuffer(presenter.DepthBuffer);
        device.MSAALevel = presenter.MSAALevel;
        device.Presenter = presenter;
        return device.BeginDraw();
    }

    [Test]
    public void AFrameIntoAPresenterWithoutSurfacesIsSkippedUntilItIsRebuilt()
    {
        var device = GpuFixture.CreateRenderDevice();
        using var presenter = new FailedRebuildPresenter(device,
            new PresentationParameters(PresenterType.RenderTarget, Size, Size, IntPtr.Zero));

        presenter.LoseSurfaces();
        Assert.That(BeginFrame(device, presenter), Is.False, "a frame began against surfaces that no longer exist");

        presenter.Resize(Size, Size);
        Assert.That(BeginFrame(device, presenter), Is.True, "the rebuilt presenter is drawn again");
        device.EndDraw();
        device.Submit();
        device.FrameEnded();
        device.DeviceWaitIdle();
    }
}
