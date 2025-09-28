using OwnerKeeper.Domain;
using OwnerKeeper.Hardware;

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

    /// <summary>Auto register metrics.</summary>
    public bool AutoRegisterMetrics { get; init; }

    /// <summary>Enable verbose debug logs.</summary>
    public bool DebugMode { get; init; }

    /// <summary>Optional hardware factory override (for tests/integration).</summary>
    public IHardwareResourceFactory? HardwareFactory { get; init; }
}

/// <summary>Timeout settings for operations (placeholder for future phases).</summary>
public sealed class OperationTimeouts
{
    /// <summary>Default timeout in seconds.</summary>
    public int DefaultSeconds { get; init; }
}
