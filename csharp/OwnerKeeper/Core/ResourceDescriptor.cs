using System.Threading;
using OwnerKeeper.Domain;
using OwnerKeeper.Hardware;

namespace OwnerKeeper.Core;

/// <summary>
/// Describes a managed resource entry in the table.
/// Contains current state, owner and an immediate lock. (SPECS §5.1)
/// </summary>
public sealed class ResourceDescriptor
{
    /// <summary>The resource identifier. (REQ-RM-001)</summary>
    public ResourceId Id { get; }

    /// <summary>Current state for monitoring. (REQ-ST-001)</summary>
    public CameraState State { get; internal set; }

    /// <summary>Current single owner (if any). (REQ-OW-001)</summary>
    public OwnerToken? CurrentOwner { get; internal set; }

    /// <summary>Bound hardware adapter for this resource.</summary>
    public IHardwareResource? Adapter { get; internal set; }

    /// <summary>
    /// Immediate occupancy lock; Wait(0) must be used for conflict detection. (REQ-RC-001)
    /// </summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    /// <summary>Create a descriptor with default state (Uninitialized).</summary>
    public ResourceDescriptor(ResourceId id)
    {
        Id = id;
        State = CameraState.Uninitialized;
    }
}
