using OwnerKeeper;

namespace OwnerKeeper.Tests;

/// <summary>
/// Phase 0 smoke test.
/// Verifies minimal behavior for REQ-OV-001 and SPECS §3.
/// </summary>
[TestClass]
public class OwnerKeeperPlaceholderTests
{
    /// <summary>
    /// Verifies Echo returns the input unchanged (REQ-OV-001, SPECS §3).
    /// </summary>
    [TestMethod]
    public void Echo_Returns_Input()
    {
        // (REQ-OV-001) Expected behavior for the initial smoke test
        var input = "hello";
        var result = OwnerKeeperPlaceholder.Echo(input);
        Assert.AreEqual(input, result);
    }
}
