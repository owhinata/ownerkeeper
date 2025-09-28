namespace OwnerKeeper.Domain;

/// <summary>
/// Error code value object. Examples: OWN2001, ARG3002. (SPECS §6.1)
/// </summary>
public readonly record struct ErrorCode(string Prefix, int Code)
{
    /// <summary>Returns a compact code like "OWN2001". (SPECS §6.1)</summary>
    public override string ToString() => $"{Prefix}{Code:0000}";
}
