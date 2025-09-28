using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OwnerKeeper.Core;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Tests.Core;

/// <summary>
/// Phase 2: ResourceManager acquire/release and conflict semantics.
/// (SCHEDULE Phase 2; REQ-OW-001, REQ-RC-001/002, REQ-ER-002)
/// </summary>
[TestClass]
public class ResourceManagerTests
{
    private static ResourceId Cam(ushort n) => new(n, ResourceKind.Camera);

    [TestMethod]
    public void Acquire_Returns_Accepted_Then_Conflicts_Return_FailedImmediately()
    {
        var rm = new ResourceManager();
        var id = Cam(1);
        var a = rm.Acquire(id, new OwnerToken("A"));
        Assert.AreEqual(OperationTicketStatus.Accepted, a.Status);

        var b = rm.Acquire(id, new OwnerToken("B"));
        Assert.AreEqual(OperationTicketStatus.FailedImmediately, b.Status);
        Assert.AreEqual("OWN2001", b.ErrorCode?.ToString());

        // Release and verify another owner can acquire
        var released = rm.Release(id, new OwnerToken("A"));
        Assert.IsTrue(released);

        var c = rm.Acquire(id, new OwnerToken("C"));
        Assert.AreEqual(OperationTicketStatus.Accepted, c.Status);
    }

    [TestMethod]
    public void Release_By_NonOwner_Fails_And_Does_Not_Unlock()
    {
        var rm = new ResourceManager();
        var id = Cam(2);
        var t = rm.Acquire(id, new OwnerToken("Owner1"));
        Assert.AreEqual(OperationTicketStatus.Accepted, t.Status);

        var releasedByOther = rm.Release(id, new OwnerToken("Other"));
        Assert.IsFalse(releasedByOther);

        // Still locked by Owner1
        var t2 = rm.Acquire(id, new OwnerToken("Other"));
        Assert.AreEqual(OperationTicketStatus.FailedImmediately, t2.Status);
    }

    [TestMethod]
    public void Parallel_Acquire_Only_One_Succeeds()
    {
        var rm = new ResourceManager();
        var id = Cam(3);

        var owners = new[] { "O1", "O2", "O3", "O4" };
        var tasks = new List<Task<OperationTicket>>();
        foreach (var o in owners)
        {
            tasks.Add(Task.Run(() => rm.Acquire(id, new OwnerToken(o))));
        }

        Task.WaitAll(tasks.ToArray());
        var tickets = tasks.Select(t => t.Result).ToList();
        var accepted = tickets.Count(t => t.Status == OperationTicketStatus.Accepted);
        var failed = tickets.Count(t =>
            t.Status == OperationTicketStatus.FailedImmediately
        );

        Assert.AreEqual(1, accepted);
        Assert.AreEqual(owners.Length - 1, failed);

        // Cleanup: release by the actual owner
        var desc = rm.TryGet(id);
        Assert.IsNotNull(desc);
        var current = desc!.CurrentOwner;
        Assert.IsTrue(current.HasValue);
        var released = rm.Release(id, current!.Value);
        Assert.IsTrue(released);
    }
}
