using System;
using System.Collections.Generic;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Core;

/// <summary>
/// Validates and applies state transitions according to ST-1 rules.
/// Misuse -> InvalidOperationException(ARG3001). Runtime preconditions (ownership)
/// failure -> OperationTicket.FailedImmediately(OWN2001). (SPECS §5.2)
/// </summary>
public sealed class StateMachine
{
    private static readonly Dictionary<
        (CameraState, OperationType),
        CameraState
    > Rules =
        new()
        {
            // Ready
            [(CameraState.Ready, OperationType.StartStreaming)] =
                CameraState.Streaming,
            [(CameraState.Ready, OperationType.UpdateConfiguration)] =
                CameraState.Ready,

            // Streaming
            [(CameraState.Streaming, OperationType.Pause)] = CameraState.Paused,
            [(CameraState.Streaming, OperationType.Stop)] = CameraState.Stopped,
            [(CameraState.Streaming, OperationType.UpdateConfiguration)] =
                CameraState.Streaming,

            // Paused
            [(CameraState.Paused, OperationType.Resume)] = CameraState.Streaming,
            [(CameraState.Paused, OperationType.Stop)] = CameraState.Stopped,

            // Stopped
            [(CameraState.Stopped, OperationType.Prepare)] = CameraState.Ready,

            // Error
            [(CameraState.Error, OperationType.Reset)] = CameraState.Ready,
        };

    /// <summary>
    /// Try to get the next state. Returns true if the transition is allowed; false otherwise.
    /// No exceptions are thrown for disallowed transitions (runtime immediate failure policy).
    /// </summary>
    public static bool TryGetNext(
        CameraState current,
        OperationType op,
        out CameraState next
    ) => Rules.TryGetValue((current, op), out next);

    /// <summary>
    /// Begin an operation by validating ownership and transition rules; update the
    /// state when accepted. Returns an OperationTicket indicating acceptance or
    /// immediate failure. (REQ-OW-001, REQ-ER-002, SPECS §5.2)
    /// </summary>
    public static OperationTicket BeginOperation(
        ResourceManager rm,
        ResourceId id,
        OwnerToken owner,
        OperationType op
    )
    {
        var desc = rm.Ensure(id);

        if (RequiresOwnership(op))
        {
            if (
                desc.CurrentOwner is not OwnerToken current
                || current.SessionId != owner.SessionId
            )
            {
                return OperationTicket.FailedImmediately(new ErrorCode("OWN", 2001));
            }
        }

        if (TryGetNext(desc.State, op, out var next))
        {
            rm.SetState(id, next);
            return OperationTicket.Accepted();
        }

        // Disallowed transition is treated as a runtime immediate failure (ARG3001)
        return OperationTicket.FailedImmediately(new ErrorCode("ARG", 3001));
    }

    private static bool RequiresOwnership(OperationType op) =>
        op != OperationType.Prepare;
}

/// <summary>Operations driving transitions. (SPECS §4.2/§5.2)</summary>
public enum OperationType
{
    /// <summary>Start streaming from Ready → Streaming.</summary>
    StartStreaming,

    /// <summary>Stop streaming from Streaming/Paused → Stopped.</summary>
    Stop,

    /// <summary>Pause streaming from Streaming → Paused.</summary>
    Pause,

    /// <summary>Resume streaming from Paused → Streaming.</summary>
    Resume,

    /// <summary>Update configuration in Ready/Streaming (state-preserving).</summary>
    UpdateConfiguration,

    /// <summary>Prepare stopped resource back to Ready.</summary>
    Prepare,

    /// <summary>Reset from Error → Ready.</summary>
    Reset,
}
