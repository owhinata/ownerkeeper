namespace OwnerKeeper.Domain;

/// <summary>
/// Strongly-typed identifier of a hardware resource.
/// (REQ-RM-001, SPECS §5.1)
/// </summary>
public readonly record struct ResourceId(ushort Value, ResourceKind Kind)
{
    /// <summary>Returns a string like "Camera:1".</summary>
    public override string ToString() => $"{Kind}:{Value}";
}

/// <summary>
/// Kinds of resources handled by the library.
/// (REQ-OV-003, SPECS §3.2)
/// </summary>
public enum ResourceKind
{
    /// <summary>Camera hardware resource. (REQ-OV-003)</summary>
    Camera = 1,
}
