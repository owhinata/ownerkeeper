using System;
using System.Threading;
using OwnerKeeper.Core;
using OwnerKeeper.Domain;

namespace OwnerKeeper.API;

/// <summary>
/// Public session API for owning and controlling a single resource.
/// Synchronous methods return OperationTicket indicating acceptance or
/// immediate failure. (SPECS §4.2)
/// </summary>
public interface IOwnerSession : IDisposable
{
    /// <summary>Stable session identifier. (REQ-AI-001)</summary>
    string SessionId { get; }

    /// <summary>Target resource identifier.</summary>
    ResourceId ResourceId { get; }

    /// <summary>Start streaming (Ready → Streaming).</summary>
    OperationTicket StartStreaming(CancellationToken cancellationToken = default);

    /// <summary>Stop streaming (Streaming/Paused → Stopped).</summary>
    OperationTicket StopStreaming(CancellationToken cancellationToken = default);

    /// <summary>Pause streaming (Streaming → Paused).</summary>
    OperationTicket PauseStreaming(CancellationToken cancellationToken = default);

    /// <summary>Resume streaming (Paused → Streaming).</summary>
    OperationTicket ResumeStreaming(CancellationToken cancellationToken = default);

    /// <summary>Update camera configuration.</summary>
    OperationTicket UpdateConfiguration(
        CameraConfiguration configuration,
        CancellationToken cancellationToken = default
    );

    /// <summary>Request current status (Phase 5: returns Accepted without event).</summary>
    OperationTicket RequestStatus(CancellationToken cancellationToken = default);

    /// <summary>Reset from Error → Ready.</summary>
    OperationTicket Reset(CancellationToken cancellationToken = default);

    /// <summary>Get the current state synchronously.</summary>
    CameraState GetCurrentState();

    /// <summary>Raised when StartStreaming completes.</summary>
    event EventHandler<OperationCompletedEventArgs>? StartStreamingCompleted;

    /// <summary>Raised when StopStreaming completes.</summary>
    event EventHandler<OperationCompletedEventArgs>? StopStreamingCompleted;

    /// <summary>Raised when PauseStreaming completes.</summary>
    event EventHandler<OperationCompletedEventArgs>? PauseStreamingCompleted;

    /// <summary>Raised when ResumeStreaming completes.</summary>
    event EventHandler<OperationCompletedEventArgs>? ResumeStreamingCompleted;

    /// <summary>Raised when UpdateConfiguration completes.</summary>
    event EventHandler<OperationCompletedEventArgs>? UpdateConfigurationCompleted;

    /// <summary>Raised when Reset completes.</summary>
    event EventHandler<OperationCompletedEventArgs>? ResetCompleted;
}
