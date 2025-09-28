using System;
using System.Threading;
using System.Threading.Tasks;
using OwnerKeeper.Core;
using OwnerKeeper.Core.Logging;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Tests.Core;

internal sealed class TestLogger : ILogger
{
    public int ErrorCount { get; private set; }

    public void Log(LogLevel level, string message)
    {
        if (level == LogLevel.Error)
            ErrorCount++;
    }
}

[TestClass]
public class OperationSchedulerTests
{
    private static ResourceId Cam(ushort n) => new(n, ResourceKind.Camera);

    [TestMethod]
    public async Task Enqueue_Success_Emits_Completion_Event()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger();
        var hub = new EventHub(logger);
        using var scheduler = new OperationScheduler(hub, rm, logger);

        var id = Cam(100);
        rm.SetState(id, CameraState.Ready);
        rm.Acquire(id, new OwnerToken("S1"));

        var tcs = new TaskCompletionSource<OperationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        hub.OperationCompleted += (s, e) =>
        {
            if (
                e.Operation == OperationType.StartStreaming
                && e.State == CameraState.Streaming
            )
                tcs.TrySetResult(e);
        };

        var ticket = scheduler.Enqueue(
            id,
            new OwnerToken("S1"),
            OperationType.StartStreaming
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, ticket.Status);

        var result = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreSame(tcs.Task, result, "No completion event received in time");
        Assert.IsTrue(tcs.Task.Result.IsSuccess);
        Assert.AreEqual(ticket.OperationId, tcs.Task.Result.OperationId);
    }

    [TestMethod]
    public async Task Enqueue_Illegal_Transition_Does_Not_Emit_Event()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger();
        var hub = new EventHub(logger);
        using var scheduler = new OperationScheduler(hub, rm, logger);

        var id = Cam(101);
        rm.SetState(id, CameraState.Streaming);
        rm.Acquire(id, new OwnerToken("S1"));

        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        hub.OperationCompleted += (s, e) => tcs.TrySetResult(true);

        var ticket = scheduler.Enqueue(
            id,
            new OwnerToken("S1"),
            OperationType.StartStreaming
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, ticket.Status);

        await Task.Delay(300); // should not emit
        Assert.IsFalse(
            tcs.Task.IsCompleted,
            "Unexpected event emitted for immediate failure"
        );
    }

    [TestMethod]
    public void Enqueue_With_PreCanceled_Token_Returns_Immediate_Failure()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger();
        var hub = new EventHub(logger);
        using var scheduler = new OperationScheduler(hub, rm, logger);

        var id = Cam(102);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ticket = scheduler.Enqueue(
            id,
            new OwnerToken("S1"),
            OperationType.StartStreaming,
            cts.Token
        );
        Assert.AreEqual(OperationTicketStatus.FailedImmediately, ticket.Status);
        Assert.AreEqual("CT0001", ticket.ErrorCode?.ToString());
    }

    [TestMethod]
    public async Task Event_Handler_Exception_Is_Logged_And_Does_Not_Stop_Dispatch()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger();
        var hub = new EventHub(logger);
        using var scheduler = new OperationScheduler(hub, rm, logger);

        var id = Cam(103);
        rm.SetState(id, CameraState.Ready);
        rm.Acquire(id, new OwnerToken("S1"));

        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        hub.OperationCompleted += (s, e) =>
            throw new InvalidOperationException("boom");
        hub.OperationCompleted += (s, e) => tcs.TrySetResult(true);

        var ticket = scheduler.Enqueue(
            id,
            new OwnerToken("S1"),
            OperationType.StartStreaming
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, ticket.Status);

        var result = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreSame(tcs.Task, result, "Second handler did not run");
        // Allow async handler to complete and logging to occur
        await Task.Delay(50);
        Assert.IsTrue(logger.ErrorCount >= 1, "Handler exception was not logged");
    }
}
