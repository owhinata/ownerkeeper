using System;
using System.Collections.Concurrent;
using System.Threading;
using OwnerKeeper.Core;
using OwnerKeeper.Domain;

namespace OwnerKeeper.API;

/// <summary>
/// Default IOwnerSession implementation.
/// Bridges synchronous API to OperationScheduler and translates completion
/// events to typed session events. (REQ-OV-001, SPECS §4.2)
/// </summary>
public sealed class OwnerSession : IOwnerSession
{
    private readonly ResourceManager _resources;
    private readonly OperationScheduler _scheduler;
    private readonly EventHub _events;
    private readonly ConcurrentDictionary<Guid, OperationType> _pending = new();
    private bool _disposed;

    /// <summary>Stable session identifier. (REQ-AI-001)</summary>
    public string SessionId { get; }

    /// <summary>Bound resource identifier for this session.</summary>
    public ResourceId ResourceId { get; }

    /// <summary>
    /// Create a new session bound to a resource and core services.
    /// </summary>
    /// <param name="sessionId">Stable session id.</param>
    /// <param name="resourceId">Target resource id.</param>
    /// <param name="resources">Resource manager.</param>
    /// <param name="scheduler">Operation scheduler.</param>
    /// <param name="events">Event hub to subscribe for completion events.</param>
    public OwnerSession(
        string sessionId,
        ResourceId resourceId,
        ResourceManager resources,
        OperationScheduler scheduler,
        EventHub events
    )
    {
        SessionId = sessionId;
        ResourceId = resourceId;
        _resources = resources;
        _scheduler = scheduler;
        _events = events;
        _events.OperationCompleted += OnOperationCompleted;
    }

    /// <summary>Return the current state synchronously.</summary>
    public CameraState GetCurrentState() => _resources.GetState(ResourceId);

    /// <summary>Start streaming (Ready → Streaming).</summary>
    public OperationTicket StartStreaming(
        CancellationToken cancellationToken = default
    ) => Enqueue(OperationType.StartStreaming, cancellationToken);

    /// <summary>Stop streaming (Streaming/Paused → Stopped).</summary>
    public OperationTicket StopStreaming(
        CancellationToken cancellationToken = default
    ) => Enqueue(OperationType.Stop, cancellationToken);

    /// <summary>Pause streaming (Streaming → Paused).</summary>
    public OperationTicket PauseStreaming(
        CancellationToken cancellationToken = default
    ) => Enqueue(OperationType.Pause, cancellationToken);

    /// <summary>Resume streaming (Paused → Streaming).</summary>
    public OperationTicket ResumeStreaming(
        CancellationToken cancellationToken = default
    ) => Enqueue(OperationType.Resume, cancellationToken);

    /// <summary>Update camera configuration.</summary>
    public OperationTicket UpdateConfiguration(
        CameraConfiguration configuration,
        CancellationToken cancellationToken = default
    ) => Enqueue(OperationType.UpdateConfiguration, cancellationToken);

    /// <summary>Reset from Error → Ready.</summary>
    public OperationTicket Reset(CancellationToken cancellationToken = default) =>
        Enqueue(OperationType.Reset, cancellationToken);

    /// <summary>Request status (Phase 5: returns Accepted without dispatch).</summary>
    public OperationTicket RequestStatus(
        CancellationToken cancellationToken = default
    ) => OperationTicket.Accepted();

    private OperationTicket Enqueue(OperationType op, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return OperationTicket.FailedImmediately(new ErrorCode("CT", 0001));

        // Immediate ownership pre-check to convert obvious runtime errors into
        // immediate failure per policy (REQ-ER-002).
        if (RequiresOwnership(op))
        {
            var desc = _resources.TryGet(ResourceId);
            if (
                desc is null
                || desc.CurrentOwner is not Core.OwnerToken current
                || current.SessionId != SessionId
            )
            {
                return OperationTicket.FailedImmediately(new ErrorCode("OWN", 2001));
            }
        }

        // Pre-validate transition to avoid queuing illegal operations (ARG3001)
        var currentState = _resources.GetState(ResourceId);
        if (!StateMachine.TryGetNext(currentState, op, out _))
        {
            return OperationTicket.FailedImmediately(new ErrorCode("ARG", 3001));
        }

        // Generate operation id first and register as pending to avoid races
        var operationId = Guid.NewGuid();
        _pending[operationId] = op;
        var ticket = _scheduler.Enqueue(
            ResourceId,
            new Core.OwnerToken(SessionId),
            op,
            operationId,
            ct
        );
        if (ticket.Status == OperationTicketStatus.FailedImmediately)
        {
            _pending.TryRemove(operationId, out _);
        }
        return ticket;
    }

    private static bool RequiresOwnership(OperationType op) =>
        op != OperationType.Prepare;

    private void OnOperationCompleted(object? sender, OperationCompletedEventArgs e)
    {
        if (
            e.ResourceId.Equals(ResourceId)
            && _pending.TryRemove(e.OperationId, out var op)
        )
        {
            switch (op)
            {
                case OperationType.StartStreaming:
                    StartStreamingCompleted?.Invoke(this, e);
                    break;
                case OperationType.Stop:
                    StopStreamingCompleted?.Invoke(this, e);
                    break;
                case OperationType.Pause:
                    PauseStreamingCompleted?.Invoke(this, e);
                    break;
                case OperationType.Resume:
                    ResumeStreamingCompleted?.Invoke(this, e);
                    break;
                case OperationType.UpdateConfiguration:
                    UpdateConfigurationCompleted?.Invoke(this, e);
                    break;
                case OperationType.Reset:
                    ResetCompleted?.Invoke(this, e);
                    break;
            }
        }
    }

    /// <summary>Raised when StartStreaming completes.</summary>
    public event EventHandler<OperationCompletedEventArgs>? StartStreamingCompleted;

    /// <summary>Raised when StopStreaming completes.</summary>
    public event EventHandler<OperationCompletedEventArgs>? StopStreamingCompleted;

    /// <summary>Raised when PauseStreaming completes.</summary>
    public event EventHandler<OperationCompletedEventArgs>? PauseStreamingCompleted;

    /// <summary>Raised when ResumeStreaming completes.</summary>
    public event EventHandler<OperationCompletedEventArgs>? ResumeStreamingCompleted;

    /// <summary>Raised when UpdateConfiguration completes.</summary>
    public event EventHandler<OperationCompletedEventArgs>? UpdateConfigurationCompleted;

    /// <summary>Raised when Reset completes.</summary>
    public event EventHandler<OperationCompletedEventArgs>? ResetCompleted;

    /// <summary>Unsubscribe from events and mark as disposed.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _events.OperationCompleted -= OnOperationCompleted;
        _disposed = true;
    }
}
