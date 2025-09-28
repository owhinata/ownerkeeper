using System;

namespace OwnerKeeper.Errors;

/// <summary>
/// Thrown when public API is used before the library is initialized. (REQ-IN-003)
/// Error code ARG3002.
/// </summary>
public sealed class OwnerKeeperNotInitializedException : InvalidOperationException
{
    /// <summary>Create exception with ARG3002 code and message.</summary>
    public OwnerKeeperNotInitializedException()
        : base("ARG3002: OwnerKeeper not initialized.") { }
}
