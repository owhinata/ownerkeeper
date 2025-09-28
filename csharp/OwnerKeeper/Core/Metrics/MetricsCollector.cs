using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace OwnerKeeper.Core.Metrics;

/// <summary>
/// Metrics collector wrapping System.Diagnostics.Metrics.
/// Exposes simple in-memory counters for tests. (REQ-MN-001)
/// </summary>
/// <summary>Collects and exposes metrics for OwnerKeeper.</summary>
public sealed class MetricsCollector : IDisposable
{
    private readonly Meter _meter = new("OwnerKeeper", "1.0.0");
    private readonly Counter<long> _opsTotal;
    private readonly Counter<long> _opsFailures;
    private readonly Histogram<double> _opLatencyMs;

    /// <summary>In-memory counter of operations per type (for tests).</summary>
    public ConcurrentDictionary<string, long> OperationsTotal { get; } = new();

    /// <summary>In-memory counter of failures per type+error (for tests).</summary>
    public ConcurrentDictionary<string, long> OperationFailures { get; } = new();

    /// <summary>Last observed latency in ms per operation type (for tests).</summary>
    public ConcurrentDictionary<string, double> LastLatencyMs { get; } = new();

    /// <summary>Create a metrics collector and instruments.</summary>
    public MetricsCollector()
    {
        _opsTotal = _meter.CreateCounter<long>("ownerkeeper_operations_total");
        _opsFailures = _meter.CreateCounter<long>(
            "ownerkeeper_operation_failures_total"
        );
        _opLatencyMs = _meter.CreateHistogram<double>(
            "ownerkeeper_operation_latency_ms"
        );
    }

    /// <summary>Record an operation occurrence with type tag.</summary>
    public void RecordOperation(string type)
    {
        OperationsTotal.AddOrUpdate(type, 1, static (_, v) => v + 1);
        _opsTotal.Add(1, KeyValuePair.Create<string, object?>("type", type));
    }

    /// <summary>Record an operation failure with tags type and error.</summary>
    public void RecordFailure(string type, string error)
    {
        var key = type + ":" + error;
        OperationFailures.AddOrUpdate(key, 1, static (_, v) => v + 1);
        _opsFailures.Add(
            1,
            KeyValuePair.Create<string, object?>("type", type),
            KeyValuePair.Create<string, object?>("error", error)
        );
    }

    /// <summary>Observe operation latency in milliseconds.</summary>
    public void ObserveLatency(string type, TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;
        LastLatencyMs[type] = ms;
        _opLatencyMs.Record(ms, KeyValuePair.Create<string, object?>("type", type));
    }

    /// <summary>Dispose underlying meter.</summary>
    public void Dispose() => _meter.Dispose();
}
