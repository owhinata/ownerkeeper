using System;
using System.Threading.Tasks;
using OwnerKeeper.Core;
using OwnerKeeper.Core.Logging;
using OwnerKeeper.Core.Metrics;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Tests.Core;

internal sealed class TestLoggerMetrics : ILogger
{
    public int InfoCount { get; private set; }
    public int ErrorCount { get; private set; }

    public void Log(LogLevel level, string message)
    {
        if (level == LogLevel.Info)
            InfoCount++;
        if (level == LogLevel.Error)
            ErrorCount++;
    }
}

[TestClass]
public class OperationMetricsTests
{
    private static ResourceId Cam(ushort n) => new(n, ResourceKind.Camera);

    [TestMethod]
    public async Task Metrics_Are_Recorded_On_Success()
    {
        var rm = new ResourceManager();
        var logger = new TestLoggerMetrics();
        var hub = new EventHub(logger);
        using var metrics = new MetricsCollector();
        using var scheduler = new OperationScheduler(hub, rm, logger, metrics);

        var id = Cam(300);
        rm.SetState(id, CameraState.Ready);
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

        var result = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreSame(tcs.Task, result, "No completion event");

        Assert.IsTrue(
            metrics.OperationsTotal.TryGetValue(
                OperationType.StartStreaming.ToString(),
                out var count
            )
                && count >= 1
        );
        Assert.IsTrue(
            metrics.LastLatencyMs.TryGetValue(
                OperationType.StartStreaming.ToString(),
                out var ms
            )
                && ms >= 0
        );
    }

    [TestMethod]
    public async Task Metrics_Failure_Recorded_On_Immediate_Failure()
    {
        var rm = new ResourceManager();
        var logger = new TestLoggerMetrics();
        var hub = new EventHub(logger);
        using var metrics = new MetricsCollector();
        using var scheduler = new OperationScheduler(hub, rm, logger, metrics);

        var id = Cam(301);
        rm.SetState(id, CameraState.Streaming);
        rm.Acquire(id, new OwnerToken("S1"));

        var ticket = scheduler.Enqueue(
            id,
            new OwnerToken("S1"),
            OperationType.StartStreaming
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, ticket.Status);

        await Task.Delay(100); // processing time
        var key = OperationType.StartStreaming + ":" + "ARG3001";
        Assert.IsTrue(
            metrics.OperationFailures.TryGetValue(key, out var fcount) && fcount >= 1
        );
    }
}
