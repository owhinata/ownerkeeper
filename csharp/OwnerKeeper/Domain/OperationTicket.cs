using System;

namespace OwnerKeeper.Domain;

/// <summary>
/// Ticket returned by synchronous API indicating acceptance or immediate failure.
/// (REQ-OV-001, REQ-ER-002, SPECS §4.2)
/// </summary>
public readonly struct OperationTicket
{
    /// <summary>Unique identifier of an operation.</summary>
    public Guid OperationId { get; }

    /// <summary>Status at issuance time.</summary>
    public OperationTicketStatus Status { get; }

    /// <summary>Error when <see cref="Status"/> is <see cref="OperationTicketStatus.FailedImmediately"/>.</summary>
    public ErrorCode? ErrorCode { get; }

    /// <summary>UTC timestamp of issuance.</summary>
    public DateTime CreatedAtUtc { get; }

    private OperationTicket(
        Guid id,
        OperationTicketStatus status,
        ErrorCode? error,
        DateTime createdAtUtc
    )
    {
        OperationId = id;
        Status = status;
        ErrorCode = error;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Create an Accepted ticket. (SPECS §4.2)</summary>
    public static OperationTicket Accepted(
        Guid? id = null,
        DateTime? nowUtc = null
    ) =>
        new(
            id ?? Guid.NewGuid(),
            OperationTicketStatus.Accepted,
            null,
            nowUtc ?? DateTime.UtcNow
        );

    /// <summary>
    /// Create an immediate failure ticket. Error code is required (REQ-ER-002).
    /// </summary>
    public static OperationTicket FailedImmediately(
        ErrorCode error,
        Guid? id = null,
        DateTime? nowUtc = null
    ) =>
        new(
            id ?? Guid.NewGuid(),
            OperationTicketStatus.FailedImmediately,
            error,
            nowUtc ?? DateTime.UtcNow
        );
}

/// <summary>Status of a freshly issued operation ticket. (SPECS §4.2)</summary>
public enum OperationTicketStatus
{
    /// <summary>Operation accepted and scheduled for async execution.</summary>
    Accepted = 0,

    /// <summary>Operation failed before async execution started. (REQ-ER-002)</summary>
    FailedImmediately = 1,
}
