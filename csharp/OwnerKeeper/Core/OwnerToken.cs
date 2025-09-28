namespace OwnerKeeper.Core;

/// <summary>
/// Lightweight owner token representing a session identity.
/// This is a Phase 2 placeholder for IOwnerSession reference. (REQ-OW-001)
/// </summary>
public readonly record struct OwnerToken(string SessionId)
{
    /// <summary>Returns a readable label for diagnostics.</summary>
    public override string ToString() => SessionId;
}
