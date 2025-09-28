using System;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Core;

/// <summary>Event args for operation completion notifications. (SPECS §4.3)</summary>
public sealed class OperationCompletedEventArgs : EventArgs
{
    /// <summary>Target resource identifier.</summary>
    public ResourceId ResourceId { get; }

    /// <summary>Operation identifier (correlates to the issued ticket).</summary>
    public Guid OperationId { get; }

    /// <summary>True when the operation completed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>The operation that completed.</summary>
    public OperationType Operation { get; }

    /// <summary>State after the operation completed.</summary>
    public CameraState State { get; }

    /// <summary>Optional metadata about the stream on success.</summary>
    public CameraMetadata? Metadata { get; }

    /// <summary>Error code on failure (if any).</summary>
    public ErrorCode? ErrorCode { get; }

    /// <summary>Event timestamp in UTC.</summary>
    public DateTime TimestampUtc { get; }

    /// <summary>Create event args for an operation completion.</summary>
    public OperationCompletedEventArgs(
        ResourceId resourceId,
        Guid operationId,
        bool isSuccess,
        OperationType operation,
        CameraState state,
        CameraMetadata? metadata,
        ErrorCode? errorCode,
        DateTime timestampUtc
    )
    {
        ResourceId = resourceId;
        OperationId = operationId;
        IsSuccess = isSuccess;
        Operation = operation;
        State = state;
        Metadata = metadata;
        ErrorCode = errorCode;
        TimestampUtc = timestampUtc;
    }
}

/// <summary>Dispatches events to subscribers with safety (logs handler exceptions).</summary>
public sealed class EventHub
{
    /// <summary>Raised when an operation completes (Phase 4 scope).</summary>
    public event EventHandler<OperationCompletedEventArgs>? OperationCompleted;

    private readonly Logging.ILogger _logger;

    /// <summary>Create an event hub with the provided logger.</summary>
    public EventHub(Logging.ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Dispatch <see cref="OperationCompleted"/> to all handlers via Task.Run.
    /// Each handler is wrapped in try/catch; exceptions are logged and suppressed.
    /// </summary>
    public void DispatchOperationCompleted(
        object sender,
        OperationCompletedEventArgs args
    )
    {
        var handlers = OperationCompleted;
        if (handlers == null)
            return;
        foreach (
            EventHandler<OperationCompletedEventArgs> handler in handlers.GetInvocationList()
        )
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    handler(sender, args);
                }
                catch (Exception ex)
                {
                    _logger.Log(
                        Logging.LogLevel.Error,
                        $"Event handler error: {ex.Message}"
                    );
                }
            });
        }
    }
}
