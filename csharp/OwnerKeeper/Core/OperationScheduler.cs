using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OwnerKeeper.Core.Logging;
using OwnerKeeper.Core.Metrics;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Core;

/// <summary>
/// Accepts synchronous operation requests and processes them asynchronously.
/// Produces completion events via <see cref="EventHub"/>. (SPECS §3.2/§4.3)
/// </summary>
public sealed class OperationScheduler : IDisposable
{
    private readonly Channel<OperationRequest> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly EventHub _events;
    private readonly ResourceManager _resources;
    private readonly ILogger _logger;
    private readonly MetricsCollector? _metrics;
    private readonly bool _debug;

    private static readonly ErrorCode Cancelled = new("CT", 0001);

    /// <summary>Create a scheduler with an unbounded channel.</summary>
    public OperationScheduler(
        EventHub events,
        ResourceManager resources,
        ILogger logger,
        MetricsCollector? metrics = null,
        bool debugMode = false
    )
    {
        _events = events;
        _resources = resources;
        _logger = logger;
        _metrics = metrics;
        _debug = debugMode;
        _channel = Channel.CreateUnbounded<OperationRequest>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );
        _worker = Task.Run(ProcessAsync);
    }

    /// <summary>
    /// Enqueue a request. Returns the ticket (Accepted or FailedImmediately).
    /// Cancellation already requested → immediate failure (CT0001). (REQ-CT-002)
    /// </summary>
    public OperationTicket Enqueue(
        ResourceId id,
        OwnerToken owner,
        OperationType op,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationTicket.FailedImmediately(Cancelled);
        }

        var ticket = OperationTicket.Accepted();
        var req = new OperationRequest(ticket.OperationId, id, owner, op);
        // Fire-and-forget write; consume ValueTask via AsTask for analyzer compliance.
        _ = _channel.Writer.WriteAsync(req, _cts.Token).AsTask();
        _logger.Log(
            LogLevel.Info,
            $"Accepted {op} id={id} opId={ticket.OperationId}"
        );
        _metrics?.RecordOperation(op.ToString());
        return ticket;
    }

    /// <summary>
    /// Enqueue a request with a pre-generated operation id. Useful to avoid
    /// race conditions when callers need to publish correlation ids before
    /// scheduling. (Stable mapping for typed events)
    /// </summary>
    public OperationTicket Enqueue(
        ResourceId id,
        OwnerToken owner,
        OperationType op,
        Guid operationId,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationTicket.FailedImmediately(Cancelled);
        }

        var ticket = OperationTicket.Accepted(operationId);
        var req = new OperationRequest(operationId, id, owner, op);
        _ = _channel.Writer.WriteAsync(req, _cts.Token).AsTask();
        _logger.Log(LogLevel.Info, $"Accepted {op} id={id} opId={operationId}");
        return ticket;
    }

    private async Task ProcessAsync()
    {
        try
        {
            while (
                await _channel
                    .Reader.WaitToReadAsync(_cts.Token)
                    .ConfigureAwait(false)
            )
            {
                while (_channel.Reader.TryRead(out var req))
                {
                    try
                    {
                        var started = DateTime.UtcNow;
                        var ticket = StateMachine.BeginOperation(
                            _resources,
                            req.Id,
                            req.Owner,
                            req.Operation
                        );
                        if (ticket.Status == OperationTicketStatus.FailedImmediately)
                        {
                            _logger.Log(
                                LogLevel.Error,
                                $"Immediate failure: {ticket.ErrorCode} for opId={req.OperationId}"
                            );
                            _metrics?.RecordFailure(
                                req.Operation.ToString(),
                                ticket.ErrorCode?.ToString() ?? "unknown"
                            );
                            continue; // No event per policy
                        }

                        try
                        {
                            var adapter = _resources.TryGet(req.Id)?.Adapter;
                            if (adapter is not null)
                            {
                                switch (req.Operation)
                                {
                                    case OperationType.StartStreaming:
                                        await adapter
                                            .StartAsync(_cts.Token)
                                            .ConfigureAwait(false);
                                        break;
                                    case OperationType.Stop:
                                        await adapter
                                            .StopAsync(_cts.Token)
                                            .ConfigureAwait(false);
                                        break;
                                    case OperationType.Pause:
                                        await adapter
                                            .PauseAsync(_cts.Token)
                                            .ConfigureAwait(false);
                                        break;
                                    case OperationType.Resume:
                                        await adapter
                                            .ResumeAsync(_cts.Token)
                                            .ConfigureAwait(false);
                                        break;
                                    case OperationType.UpdateConfiguration:
                                        await adapter
                                            .UpdateConfigurationAsync(
                                                new CameraConfiguration(
                                                    new CameraResolution(1920, 1080),
                                                    PixelFormat.Rgb24,
                                                    new FrameRate(30)
                                                ),
                                                _cts.Token
                                            )
                                            .ConfigureAwait(false);
                                        break;
                                }
                            }

                            var state = _resources.GetState(req.Id);
                            var args = new OperationCompletedEventArgs(
                                req.Id,
                                req.OperationId,
                                true,
                                req.Operation,
                                state,
                                metadata: null,
                                errorCode: null,
                                timestampUtc: DateTime.UtcNow
                            );
                            _events.DispatchOperationCompleted(this, args);
                            _metrics?.ObserveLatency(
                                req.Operation.ToString(),
                                DateTime.UtcNow - started
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(
                                LogLevel.Error,
                                $"Hardware op failed: {ex.Message}"
                            );
                            var state = _resources.GetState(req.Id);
                            var args = new OperationCompletedEventArgs(
                                req.Id,
                                req.OperationId,
                                false,
                                req.Operation,
                                state,
                                metadata: null,
                                errorCode: new ErrorCode("HW", 1001),
                                timestampUtc: DateTime.UtcNow
                            );
                            _events.DispatchOperationCompleted(this, args);
                            _metrics?.RecordFailure(
                                req.Operation.ToString(),
                                "HW1001"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, $"Scheduler error: {ex.Message}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>Stop background processing.</summary>
    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _worker.Wait(2000);
        }
        catch
        { /* ignore */
        }
        _cts.Dispose();
    }

    private readonly record struct OperationRequest(
        Guid OperationId,
        ResourceId Id,
        OwnerToken Owner,
        OperationType Operation
    );
}
