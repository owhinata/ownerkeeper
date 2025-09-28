using System;
using System.Threading;
using System.Threading.Tasks;
using OwnerKeeper.API;
using OwnerKeeper.Core;
using OwnerKeeper.Core.Logging;
using OwnerKeeper.Domain;
using OwnerKeeper.Hardware;

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
            cancellationToken: cts.Token
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

    [TestMethod]
    public async Task UpdateConfiguration_Uses_Request_Configuration()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger();
        var hub = new EventHub(logger);
        var adapter = new RecordingAdapter();
        var id = Cam(200);
        rm.RegisterAdapter(id, adapter);
        rm.SetState(id, CameraState.Ready);
        rm.Acquire(id, new OwnerToken("S1"));

        using var scheduler = new OperationScheduler(hub, rm, logger);

        var tcs = new TaskCompletionSource<OperationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        hub.OperationCompleted += (s, e) =>
        {
            if (e.Operation == OperationType.UpdateConfiguration)
            {
                tcs.TrySetResult(e);
            }
        };

        var config = new CameraConfiguration(
            new CameraResolution(640, 360),
            PixelFormat.Yuv420,
            new FrameRate(24)
        );

        var ticket = scheduler.Enqueue(
            id,
            new OwnerToken("S1"),
            OperationType.UpdateConfiguration,
            configuration: config,
            cancellationToken: default
        );

        Assert.AreEqual(OperationTicketStatus.Accepted, ticket.Status);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreSame(
            tcs.Task,
            completed,
            "UpdateConfiguration did not complete in time"
        );
        Assert.IsTrue(tcs.Task.Result.IsSuccess);
        Assert.IsNotNull(adapter.LastConfiguration);
        Assert.AreEqual(
            config.Resolution,
            adapter.LastConfiguration!.Resolution,
            "Resolution was not forwarded"
        );
        Assert.AreEqual(config.PixelFormat, adapter.LastConfiguration.PixelFormat);
        Assert.AreEqual(config.FrameRate, adapter.LastConfiguration.FrameRate);
    }

    [TestMethod]
    public async Task StartStreaming_Timeout_Raises_Failure()
    {
        var rm = new ResourceManager();
        var logger = new TestLogger();
        var hub = new EventHub(logger);
        var adapter = new SlowAdapter(TimeSpan.FromMilliseconds(200));
        var id = Cam(201);
        rm.RegisterAdapter(id, adapter);
        rm.SetState(id, CameraState.Ready);
        rm.Acquire(id, new OwnerToken("S1"));

        var timeouts = new OperationTimeouts
        {
            StartStreaming = TimeSpan.FromMilliseconds(40),
        };

        using var scheduler = new OperationScheduler(
            hub,
            rm,
            logger,
            metrics: null,
            debugMode: false,
            defaultConfiguration: null,
            timeouts: timeouts
        );

        var tcs = new TaskCompletionSource<OperationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        hub.OperationCompleted += (s, e) =>
        {
            if (e.Operation == OperationType.StartStreaming)
            {
                tcs.TrySetResult(e);
            }
        };

        var ticket = scheduler.Enqueue(
            id,
            new OwnerToken("S1"),
            OperationType.StartStreaming
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, ticket.Status);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.AreSame(tcs.Task, completed, "Timeout result not observed");
        Assert.IsFalse(tcs.Task.Result.IsSuccess);
        Assert.AreEqual("CT0002", tcs.Task.Result.ErrorCode?.ToString());
    }

    private sealed class RecordingAdapter : IHardwareResource
    {
        public CameraConfiguration? LastConfiguration { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateConfigurationAsync(
            CameraConfiguration configuration,
            CancellationToken cancellationToken
        )
        {
            LastConfiguration = configuration;
            return Task.CompletedTask;
        }
    }

    private sealed class SlowAdapter : IHardwareResource
    {
        private readonly TimeSpan _delay;

        public SlowAdapter(TimeSpan delay)
        {
            _delay = delay;
        }

        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.Delay(_delay, cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateConfigurationAsync(
            CameraConfiguration configuration,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }
}
