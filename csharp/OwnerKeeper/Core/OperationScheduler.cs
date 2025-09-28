using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OwnerKeeper.Core.Logging;
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

    private static readonly ErrorCode Cancelled = new("CT", 0001);

    /// <summary>Create a scheduler with an unbounded channel.</summary>
    public OperationScheduler(
        EventHub events,
        ResourceManager resources,
        ILogger logger
    )
    {
        _events = events;
        _resources = resources;
        _logger = logger;
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
                            continue; // No event per policy
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
