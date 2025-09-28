using System;
using OwnerKeeper.Core;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Tests.Core;

/// <summary>
/// Phase 3: StateMachine transition validation and outcomes.
/// - Valid transitions update state.
/// - Illegal transitions become immediate failure (ARG3001).
/// - Ownership precondition failure returns FailedImmediately(OWN2001).
/// </summary>
[TestClass]
public class StateMachineTests
{
    private static ResourceId Cam(ushort n) => new(n, ResourceKind.Camera);

    [TestMethod]
    public void Valid_Transitions_Update_State()
    {
        var rm = new ResourceManager();
        var id = Cam(10);
        rm.SetState(id, CameraState.Ready);
        rm.Acquire(id, new OwnerToken("A"));

        var t1 = StateMachine.BeginOperation(
            rm,
            id,
            new OwnerToken("A"),
            OperationType.StartStreaming
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, t1.Status);
        Assert.AreEqual(CameraState.Streaming, rm.GetState(id));

        var t2 = StateMachine.BeginOperation(
            rm,
            id,
            new OwnerToken("A"),
            OperationType.Pause
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, t2.Status);
        Assert.AreEqual(CameraState.Paused, rm.GetState(id));

        var t3 = StateMachine.BeginOperation(
            rm,
            id,
            new OwnerToken("A"),
            OperationType.Resume
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, t3.Status);
        Assert.AreEqual(CameraState.Streaming, rm.GetState(id));
    }

    [TestMethod]
    public void Illegal_Transition_Returns_Immediate_Failure_With_ARG3001()
    {
        var rm = new ResourceManager();
        var id = Cam(11);
        rm.SetState(id, CameraState.Streaming);
        rm.Acquire(id, new OwnerToken("A"));

        var ticket = StateMachine.BeginOperation(
            rm,
            id,
            new OwnerToken("A"),
            OperationType.StartStreaming
        );
        Assert.AreEqual(OperationTicketStatus.FailedImmediately, ticket.Status);
        Assert.AreEqual("ARG3001", ticket.ErrorCode?.ToString());
    }

    [TestMethod]
    public void Ownership_Precondition_Failure_Returns_Immediate_Failure()
    {
        var rm = new ResourceManager();
        var id = Cam(12);
        rm.SetState(id, CameraState.Ready);
        rm.Acquire(id, new OwnerToken("A"));

        var ticket = StateMachine.BeginOperation(
            rm,
            id,
            new OwnerToken("B"),
            OperationType.StartStreaming
        );
        Assert.AreEqual(OperationTicketStatus.FailedImmediately, ticket.Status);
        Assert.AreEqual("OWN2001", ticket.ErrorCode?.ToString());
        Assert.AreEqual(CameraState.Ready, rm.GetState(id));
    }
}
