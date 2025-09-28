using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OwnerKeeper.API;
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
    private readonly CameraConfiguration _defaultConfiguration;
    private readonly OperationTimeouts _timeouts;

    private static readonly ErrorCode Cancelled = new("CT", 0001);
    private static readonly ErrorCode TimeoutError = new("CT", 0002);
    private static readonly ErrorCode HardwareFailure = new("HW", 1001);

    /// <summary>Create a scheduler with an unbounded channel.</summary>
    public OperationScheduler(
        EventHub events,
        ResourceManager resources,
        ILogger logger,
        MetricsCollector? metrics = null,
        bool debugMode = false,
        CameraConfiguration? defaultConfiguration = null,
        OperationTimeouts? timeouts = null
    )
    {
        _events = events;
        _resources = resources;
        _logger = logger;
        _metrics = metrics;
        _debug = debugMode;
        _defaultConfiguration =
            defaultConfiguration
            ?? new CameraConfiguration(
                new CameraResolution(1920, 1080),
                PixelFormat.Rgb24,
                new FrameRate(30)
            );
        _timeouts = timeouts ?? OperationTimeouts.CreateDefault();
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
        CameraConfiguration? configuration = null,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationTicket.FailedImmediately(Cancelled);
        }

        var ticket = OperationTicket.Accepted();
        var req = new OperationRequest(
            ticket.OperationId,
            id,
            owner,
            op,
            configuration,
            cancellationToken
        );
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
        CameraConfiguration? configuration = null,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationTicket.FailedImmediately(Cancelled);
        }

        var ticket = OperationTicket.Accepted(operationId);
        var req = new OperationRequest(
            operationId,
            id,
            owner,
            op,
            configuration,
            cancellationToken
        );
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

                        using var cancellationScope = CreateCancellationScope(req);
                        try
                        {
                            var adapter = _resources.TryGet(req.Id)?.Adapter;
                            if (adapter is not null)
                            {
                                switch (req.Operation)
                                {
                                    case OperationType.StartStreaming:
                                        await adapter
                                            .StartAsync(cancellationScope.Token)
                                            .ConfigureAwait(false);
                                        break;
                                    case OperationType.Stop:
                                        await adapter
                                            .StopAsync(cancellationScope.Token)
                                            .ConfigureAwait(false);
                                        break;
                                    case OperationType.Pause:
                                        await adapter
                                            .PauseAsync(cancellationScope.Token)
                                            .ConfigureAwait(false);
                                        break;
                                    case OperationType.Resume:
                                        await adapter
                                            .ResumeAsync(cancellationScope.Token)
                                            .ConfigureAwait(false);
                                        break;
                                    case OperationType.UpdateConfiguration:
                                        await adapter
                                            .UpdateConfigurationAsync(
                                                ResolveConfiguration(req),
                                                cancellationScope.Token
                                            )
                                            .ConfigureAwait(false);
                                        break;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            HandleCancellationOrTimeout(req, cancellationScope);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            HandleHardwareFailure(req, ex);
                            continue;
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

    private CameraConfiguration ResolveConfiguration(OperationRequest request) =>
        request.Configuration ?? _defaultConfiguration;

    private OperationCancellationScope CreateCancellationScope(
        OperationRequest request
    )
    {
        var tokens = new List<CancellationToken> { _cts.Token };

        if (request.CancellationToken.CanBeCanceled)
        {
            tokens.Add(request.CancellationToken);
        }

        var timeout = ResolveTimeout(request.Operation);
        CancellationTokenSource? timeoutCts = null;
        if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCts = new CancellationTokenSource(timeout);
            tokens.Add(timeoutCts.Token);
        }

        CancellationTokenSource? linkedCts = null;
        CancellationToken token;
        if (tokens.Count == 1)
        {
            token = tokens[0];
        }
        else
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                tokens.ToArray()
            );
            token = linkedCts.Token;
        }

        return new OperationCancellationScope(timeoutCts, linkedCts, token);
    }

    private TimeSpan ResolveTimeout(OperationType operation) =>
        operation switch
        {
            OperationType.StartStreaming => _timeouts.StartStreaming,
            OperationType.Stop => _timeouts.Stop,
            OperationType.Pause => _timeouts.Pause,
            OperationType.Resume => _timeouts.Resume,
            OperationType.UpdateConfiguration => _timeouts.UpdateConfiguration,
            OperationType.Reset => _timeouts.Reset,
            _ => _timeouts.Default,
        };

    private void HandleCancellationOrTimeout(
        OperationRequest request,
        OperationCancellationScope scope
    )
    {
        var isTimeout = scope.IsTimeout;
        var error = isTimeout ? TimeoutError : Cancelled;
        var logLevel = isTimeout ? LogLevel.Error : LogLevel.Warning;
        var message = isTimeout
            ? $"Operation {request.Operation} timed out after {ResolveTimeout(request.Operation)} for opId={request.OperationId}"
            : $"Operation {request.Operation} cancelled for opId={request.OperationId}";
        _logger.Log(logLevel, message); // (REQ-CT-002) Trace cancellation/timeout.

        var state = _resources.GetState(request.Id);
        var args = new OperationCompletedEventArgs(
            request.Id,
            request.OperationId,
            false,
            request.Operation,
            state,
            metadata: null,
            errorCode: error,
            timestampUtc: DateTime.UtcNow
        );
        _events.DispatchOperationCompleted(this, args);
        _metrics?.RecordFailure(request.Operation.ToString(), error.ToString());
    }

    private void HandleHardwareFailure(OperationRequest request, Exception ex)
    {
        _logger.Log(LogLevel.Error, $"Hardware op failed: {ex.Message}");
        var state = _resources.GetState(request.Id);
        var args = new OperationCompletedEventArgs(
            request.Id,
            request.OperationId,
            false,
            request.Operation,
            state,
            metadata: null,
            errorCode: HardwareFailure,
            timestampUtc: DateTime.UtcNow
        );
        _events.DispatchOperationCompleted(this, args);
        _metrics?.RecordFailure(
            request.Operation.ToString(),
            HardwareFailure.ToString()
        );
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
        OperationType Operation,
        CameraConfiguration? Configuration,
        CancellationToken CancellationToken
    );

    private readonly struct OperationCancellationScope : IDisposable
    {
        private readonly CancellationTokenSource? _timeoutCts;
        private readonly CancellationTokenSource? _linkedCts;

        public OperationCancellationScope(
            CancellationTokenSource? timeoutCts,
            CancellationTokenSource? linkedCts,
            CancellationToken token
        )
        {
            Token = token;
            _timeoutCts = timeoutCts;
            _linkedCts = linkedCts;
        }

        public CancellationToken Token { get; }

        public bool IsTimeout => _timeoutCts?.IsCancellationRequested == true;

        public void Dispose()
        {
            _timeoutCts?.Dispose();
            _linkedCts?.Dispose();
        }
    }
}
