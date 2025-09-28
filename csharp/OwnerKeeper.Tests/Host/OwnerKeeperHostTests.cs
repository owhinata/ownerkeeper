using System;
using System.Threading.Tasks;
using OwnerKeeper;
using OwnerKeeper.API;
using OwnerKeeper.Domain;
using OwnerKeeper.Errors;

namespace OwnerKeeper.Tests.Host;

[TestClass]
public class OwnerKeeperHostTests
{
    [TestInitialize]
    public void Reset() => OwnerKeeperHost.Instance.Shutdown();

    [TestMethod]
    public void CreateSession_Before_Initialize_Throws_NotInitialized()
    {
        Assert.ThrowsExactly<OwnerKeeperNotInitializedException>(
            () => OwnerKeeperHost.Instance.CreateSession("U1")
        );
    }

    [TestMethod]
    public void Initialize_Is_Idempotent()
    {
        OwnerKeeperHost.Instance.Initialize(
            new OwnerKeeperOptions { CameraCount = 1 }
        );
        OwnerKeeperHost.Instance.Initialize(
            new OwnerKeeperOptions { CameraCount = 1 }
        );
    }

    [TestMethod]
    public async Task CreateSession_Acquires_And_StartStreaming_Works()
    {
        OwnerKeeperHost.Instance.Initialize(
            new OwnerKeeperOptions { CameraCount = 1 }
        );
        var session = OwnerKeeperHost.Instance.CreateSession("U1");

        var ticket = session.StartStreaming();
        Assert.AreEqual(OperationTicketStatus.Accepted, ticket.Status);

        // Poll state until Streaming to avoid flakiness in event timing in CI.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (
            session.GetCurrentState() != CameraState.Streaming
            && DateTime.UtcNow < deadline
        )
        {
            await Task.Delay(50);
        }
        Assert.AreEqual(
            CameraState.Streaming,
            session.GetCurrentState(),
            "State did not become Streaming in time"
        );
    }

    [TestMethod]
    public void Shutdown_Disallows_Further_Session_Creation()
    {
        OwnerKeeperHost.Instance.Initialize(
            new OwnerKeeperOptions { CameraCount = 1 }
        );
        OwnerKeeperHost.Instance.Shutdown();
        Assert.ThrowsExactly<OwnerKeeperNotInitializedException>(
            () => OwnerKeeperHost.Instance.CreateSession("U2")
        );
    }
}
