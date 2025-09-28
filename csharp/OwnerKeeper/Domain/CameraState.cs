namespace OwnerKeeper.Domain;

/// <summary>
/// Logical camera states used by API and events.
/// (REQ-ST-001, SPECS §3.2)
/// </summary>
public enum CameraState
{
    /// <summary>Not initialized yet. (REQ-ST-001)</summary>
    Uninitialized = 0,

    /// <summary>Initialization in progress. (REQ-ST-001)</summary>
    Initializing = 1,

    /// <summary>Ready to start streaming or change settings. (REQ-ST-003)</summary>
    Ready = 2,

    /// <summary>Actively streaming frames. (REQ-ST-003)</summary>
    Streaming = 3,

    /// <summary>Streaming is paused. (REQ-ST-003)</summary>
    Paused = 4,

    /// <summary>Streaming stopped. (REQ-ST-003)</summary>
    Stopped = 5,

    /// <summary>Error state; recovery or reinit required. (REQ-ST-003)</summary>
    Error = 6,
}
