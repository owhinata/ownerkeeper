using System;
using System.Threading;
using System.Threading.Tasks;
using OwnerKeeper.API;
using OwnerKeeper.Core;
using OwnerKeeper.Core.Logging;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Tests.API;

internal sealed class TestLogger2 : ILogger
{
    public void Log(LogLevel level, string message) { }
}

[TestClass]
public class OwnerSessionTests
{
    private static ResourceId Cam(ushort n) => new(n, ResourceKind.Camera);

    [TestMethod]
    public async Task StartStreaming_Accepted_Raises_Typed_Event()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger2();
        var hub = new EventHub(logger);
        using var scheduler = new OperationScheduler(hub, rm, logger);
        var id = Cam(200);
        rm.SetState(id, CameraState.Ready);
        rm.Acquire(id, new OwnerToken("S1"));

        using var session = new OwnerSession("S1", id, rm, scheduler, hub);
        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        session.StartStreamingCompleted += (s, e) => tcs.TrySetResult(true);

        var ticket = session.StartStreaming();
        Assert.AreEqual(OperationTicketStatus.Accepted, ticket.Status);
        var result = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreSame(tcs.Task, result, "No StartStreamingCompleted event");
    }

    [TestMethod]
    public void StartStreaming_Without_Ownership_Fails_Immediately()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger2();
        var hub = new EventHub(logger);
        using var scheduler = new OperationScheduler(hub, rm, logger);
        var id = Cam(201);
        rm.SetState(id, CameraState.Ready);
        using var session = new OwnerSession("S1", id, rm, scheduler, hub);

        var ticket = session.StartStreaming();
        Assert.AreEqual(OperationTicketStatus.FailedImmediately, ticket.Status);
        Assert.AreEqual("OWN2001", ticket.ErrorCode?.ToString());
    }

    [TestMethod]
    public void GetCurrentState_Returns_Resource_State()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger2();
        var hub = new EventHub(logger);
        using var scheduler = new OperationScheduler(hub, rm, logger);
        var id = Cam(202);
        rm.SetState(id, CameraState.Ready);
        using var session = new OwnerSession("S1", id, rm, scheduler, hub);

        Assert.AreEqual(CameraState.Ready, session.GetCurrentState());
    }

    [TestMethod]
    public void PreCanceled_Token_Fails_Immediately()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger2();
        var hub = new EventHub(logger);
        using var scheduler = new OperationScheduler(hub, rm, logger);
        var id = Cam(203);
        rm.SetState(id, CameraState.Ready);
        using var session = new OwnerSession("S1", id, rm, scheduler, hub);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var ticket = session.StartStreaming(cts.Token);
        Assert.AreEqual(OperationTicketStatus.FailedImmediately, ticket.Status);
        Assert.AreEqual("CT0001", ticket.ErrorCode?.ToString());
    }
}
