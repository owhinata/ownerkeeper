using OwnerKeeper.Domain;

namespace OwnerKeeper.API;

/// <summary>Options for initializing OwnerKeeper. (SPECS §7)</summary>
public sealed class OwnerKeeperOptions
{
    /// <summary>Number of camera resources to pre-register.</summary>
    public int CameraCount { get; init; } = 1;

    /// <summary>Default configuration (Phase 5 placeholder).</summary>
    public CameraConfiguration? DefaultConfiguration { get; init; }

    /// <summary>Operation timeouts (placeholder).</summary>
    public OperationTimeouts? Timeouts { get; init; }

    /// <summary>Auto register metrics (not used in Phase 5/6).</summary>
    /// <summary>Auto register metrics (no-op in current phase).</summary>
    public bool AutoRegisterMetrics { get; init; }
}

/// <summary>Timeout settings for operations (placeholder for future phases).</summary>
public sealed class OperationTimeouts
{
    /// <summary>Default timeout in seconds.</summary>
    public int DefaultSeconds { get; init; }
}
