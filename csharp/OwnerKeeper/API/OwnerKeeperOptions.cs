using System;
using OwnerKeeper.Domain;
using OwnerKeeper.Hardware;

namespace OwnerKeeper.API;

/// <summary>Options for initializing OwnerKeeper. (SPECS §7)</summary>
public sealed class OwnerKeeperOptions
{
    /// <summary>Number of camera resources to pre-register.</summary>
    public int CameraCount { get; init; } = 1;

    /// <summary>
    /// Default configuration when callers do not supply one. (REQ-CF-004, SPECS §7)
    /// </summary>
    public CameraConfiguration? DefaultConfiguration { get; init; }

    /// <summary>Operation timeout profile. (REQ-CT-002, SPECS §4.4)</summary>
    public OperationTimeouts? Timeouts { get; init; }

    /// <summary>Auto register metrics.</summary>
    public bool AutoRegisterMetrics { get; init; }

    /// <summary>Enable verbose debug logs.</summary>
    public bool DebugMode { get; init; }

    /// <summary>Optional hardware factory override (for tests/integration).</summary>
    public IHardwareResourceFactory? HardwareFactory { get; init; }
}

/// <summary>Timeout settings per operation. (REQ-CT-002, SPECS §4.4)</summary>
public sealed class OperationTimeouts
{
    /// <summary>Fallback timeout for unspecified operations.</summary>
    public TimeSpan Default { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Timeout for StartStreaming operations.</summary>
    public TimeSpan StartStreaming { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Timeout for Stop operations.</summary>
    public TimeSpan Stop { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Timeout for Pause operations.</summary>
    public TimeSpan Pause { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Timeout for Resume operations.</summary>
    public TimeSpan Resume { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Timeout for UpdateConfiguration operations.</summary>
    public TimeSpan UpdateConfiguration { get; init; } = TimeSpan.FromSeconds(4);

    /// <summary>Timeout for Reset operations.</summary>
    public TimeSpan Reset { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Create a new instance with default timeout values.</summary>
    public static OperationTimeouts CreateDefault() => new();
}
