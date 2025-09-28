using System;
using System.Collections.Concurrent;
using System.Threading;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Core;

/// <summary>
/// Thread-safe resource table and ownership management.
/// - Single ownership per resource (REQ-OW-001)
/// - Immediate conflict detection via SemaphoreSlim.Wait(0) (REQ-RC-001)
/// - Returns OperationTicket for acquire attempts (REQ-ER-002, REQ-RC-002)
/// </summary>
public sealed class ResourceManager : IDisposable
{
    private readonly ConcurrentDictionary<ResourceId, ResourceDescriptor> _table =
        new();
    private readonly ReaderWriterLockSlim _rw = new(LockRecursionPolicy.NoRecursion);

    private static readonly ErrorCode OwnershipConflict = new("OWN", 2001); // (REQ-RC-001/002)

    /// <summary>Ensure a descriptor exists for the given id; returns the descriptor.</summary>
    public ResourceDescriptor Ensure(ResourceId id)
    {
        _rw.EnterUpgradeableReadLock();
        try
        {
            if (_table.TryGetValue(id, out var existing))
                return existing;
            _rw.EnterWriteLock();
            try
            {
                return _table.GetOrAdd(id, static key => new ResourceDescriptor(key));
            }
            finally
            {
                _rw.ExitWriteLock();
            }
        }
        finally
        {
            _rw.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Try to acquire ownership for the session. Returns a ticket indicating
    /// acceptance or immediate failure (conflict). The caller must call Release
    /// to free the lock when done. (REQ-OW-001, REQ-RC-001/002)
    /// </summary>
    public OperationTicket Acquire(ResourceId id, OwnerToken owner)
    {
        var desc = Ensure(id);

        // Immediate occupancy check; no waiting allowed per REQ-RC-001.
        if (!desc.Lock.Wait(0))
        {
            return OperationTicket.FailedImmediately(OwnershipConflict);
        }

        bool success = false;
        _rw.EnterWriteLock();
        try
        {
            if (desc.CurrentOwner is null)
            {
                desc.CurrentOwner = owner;
                success = true;
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }

        if (success)
        {
            return OperationTicket.Accepted();
        }

        // Edge case: semaphore acquired but owner was already set (should not happen
        // in normal flow). Release semaphore and fail.
        desc.Lock.Release();
        return OperationTicket.FailedImmediately(OwnershipConflict);
    }

    /// <summary>
    /// Release ownership if held by the specified owner. Returns true on success.
    /// </summary>
    public bool Release(ResourceId id, OwnerToken owner)
    {
        if (!_table.TryGetValue(id, out var desc))
            return false;

        bool released = false;
        _rw.EnterWriteLock();
        try
        {
            if (
                desc.CurrentOwner is OwnerToken current
                && current.SessionId == owner.SessionId
            )
            {
                desc.CurrentOwner = null;
                released = true;
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }

        if (released)
        {
            // Release semaphore after clearing owner.
            desc.Lock.Release();
        }

        return released;
    }

    /// <summary>Returns the descriptor if registered, otherwise null.</summary>
    public ResourceDescriptor? TryGet(ResourceId id) =>
        _table.TryGetValue(id, out var d) ? d : null;

    /// <summary>
    /// Set the state of a resource under write lock. Creates the descriptor if
    /// not present. Intended for initialization and controlled transitions.
    /// (SPECS §5.2)
    /// </summary>
    public void SetState(ResourceId id, CameraState next)
    {
        _rw.EnterWriteLock();
        try
        {
            var desc = _table.GetOrAdd(id, static key => new ResourceDescriptor(key));
            desc.State = next;
        }
        finally
        {
            _rw.ExitWriteLock();
        }
    }

    /// <summary>Get the current state; returns Uninitialized if not registered.</summary>
    public CameraState GetState(ResourceId id)
    {
        _rw.EnterReadLock();
        try
        {
            return _table.TryGetValue(id, out var desc)
                ? desc.State
                : CameraState.Uninitialized;
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    /// <summary>Dispose managed resources (locks).</summary>
    public void Dispose()
    {
        _rw.Dispose();
        foreach (var kv in _table)
        {
            kv.Value.Lock.Dispose();
        }
    }
}
