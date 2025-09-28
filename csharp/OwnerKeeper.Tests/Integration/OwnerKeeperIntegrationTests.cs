using System;
using System.Threading.Tasks;
using OwnerKeeper;
using OwnerKeeper.API;
using OwnerKeeper.Domain;
using OwnerKeeper.Hardware;

namespace OwnerKeeper.Tests.Integration;

internal sealed class FailingStartFactory : IHardwareResourceFactory
{
    private sealed class FailingStartAdapter : IHardwareResource
    {
        public Task StartAsync(
            System.Threading.CancellationToken cancellationToken
        ) => throw new InvalidOperationException("start failed");

        public Task StopAsync(System.Threading.CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PauseAsync(
            System.Threading.CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task ResumeAsync(
            System.Threading.CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task UpdateConfigurationAsync(
            CameraConfiguration configuration,
            System.Threading.CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }

    public IHardwareResource Create(ResourceId id) => new FailingStartAdapter();
}

[TestClass]
public class OwnerKeeperIntegrationTests
{
    [TestInitialize]
    public void Reset() => OwnerKeeperHost.Instance.Shutdown();

    [TestMethod]
    public async Task Normal_Flow_Start_Pause_Resume_Stop()
    {
        OwnerKeeperHost.Instance.Initialize(
            new OwnerKeeperOptions { CameraCount = 1 }
        );
        using var session = OwnerKeeperHost.Instance.CreateSession("U-NORMAL");

        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        session.StartStreamingCompleted += (s, e) => tcs.TrySetResult(true);
        var t = session.StartStreaming();
        Assert.AreEqual(OperationTicketStatus.Accepted, t.Status);
        var r = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreSame(tcs.Task, r, "No start completion");

        Assert.AreEqual(CameraState.Streaming, session.GetCurrentState());

        session.PauseStreaming();
        await Task.Delay(50);
        Assert.AreEqual(CameraState.Paused, session.GetCurrentState());

        session.ResumeStreaming();
        await Task.Delay(50);
        Assert.AreEqual(CameraState.Streaming, session.GetCurrentState());

        session.StopStreaming();
        await Task.Delay(50);
        Assert.AreEqual(CameraState.Stopped, session.GetCurrentState());
    }

    [TestMethod]
    public void Conflict_CreateSession_Throws_When_No_Resource_Available()
    {
        OwnerKeeperHost.Instance.Initialize(
            new OwnerKeeperOptions { CameraCount = 1 }
        );
        var s1 = OwnerKeeperHost.Instance.CreateSession("U1");
        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(
                () => OwnerKeeperHost.Instance.CreateSession("U2")
            );
        }
        finally
        {
            s1.Dispose();
        }
    }

    [TestMethod]
    public async Task Hardware_Error_Raises_Failure_Event()
    {
        OwnerKeeperHost.Instance.Initialize(
            new OwnerKeeperOptions
            {
                CameraCount = 1,
                HardwareFactory = new FailingStartFactory()
            }
        );
        using var session = OwnerKeeperHost.Instance.CreateSession("U-FAIL");
        var tcs = new TaskCompletionSource<(bool ok, string? code)>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        session.StartStreamingCompleted += (s, e) =>
            tcs.TrySetResult((e.IsSuccess, e.ErrorCode?.ToString()));
        var t = session.StartStreaming();
        Assert.AreEqual(OperationTicketStatus.Accepted, t.Status);
        var r = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreSame(tcs.Task, r, "No start completion");
        var (ok, code) = tcs.Task.Result;
        Assert.IsFalse(ok);
        Assert.AreEqual("HW1001", code);
    }
}
