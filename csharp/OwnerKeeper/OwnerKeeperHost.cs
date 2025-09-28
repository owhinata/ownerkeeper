using System;
using OwnerKeeper.API;
using OwnerKeeper.Core;
using OwnerKeeper.Core.Logging;
using OwnerKeeper.Domain;
using OwnerKeeper.Errors;

namespace OwnerKeeper;

/// <summary>
/// Library entry point and lifecycle manager. (SPECS §4.1, REQ-IN-001~005)
/// </summary>
public sealed class OwnerKeeperHost : IDisposable
{
    private static readonly OwnerKeeperHost _instance = new();

    /// <summary>Singleton instance of the host.</summary>
    public static OwnerKeeperHost Instance => _instance;

    private readonly object _gate = new();
    private bool _initialized;
    private OwnerKeeperOptions? _options;

    private ILogger? _logger;
    private EventHub? _events;
    private ResourceManager? _resources;
    private OperationScheduler? _scheduler;

    private OwnerKeeperHost() { }

    /// <summary>Initialize once; subsequent calls are idempotent. (REQ-IN-004)</summary>
    public void Initialize(OwnerKeeperOptions options)
    {
        lock (_gate)
        {
            if (_initialized)
                return;

            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = new ConsoleLogger();
            _events = new EventHub(_logger);
            _resources = new ResourceManager();
            _scheduler = new OperationScheduler(_events, _resources, _logger);

            // Pre-register resources and set initial state to Ready.
            var count = Math.Max(0, options.CameraCount);
            for (ushort i = 1; i <= count; i++)
            {
                var id = new ResourceId(i, ResourceKind.Camera);
                _resources.SetState(id, CameraState.Ready);
            }

            _initialized = true;
        }
    }

    /// <summary>Shut down services and release resources. Idempotent. (REQ-IN-002)</summary>
    public void Shutdown()
    {
        lock (_gate)
        {
            if (!_initialized)
                return;
            _scheduler?.Dispose();
            _scheduler = null;
            _resources?.Dispose();
            _resources = null;
            _events = null;
            _logger = null;
            _options = null;
            _initialized = false;
        }
    }

    /// <summary>Create an owner session bound to a free resource. (REQ-AI-001)</summary>
    /// <exception cref="OwnerKeeperNotInitializedException">When called before Initialize</exception>
    /// <exception cref="InvalidOperationException">When no resource is available (OWN2001)</exception>
    public API.IOwnerSession CreateSession(string? userId = null)
    {
        lock (_gate)
        {
            if (
                !_initialized
                || _resources is null
                || _scheduler is null
                || _events is null
            )
                throw new OwnerKeeperNotInitializedException();

            var sid = userId ?? Guid.NewGuid().ToString("N");
            var count = Math.Max(0, _options?.CameraCount ?? 0);
            for (ushort i = 1; i <= count; i++)
            {
                var id = new ResourceId(i, ResourceKind.Camera);
                var ticket = _resources.Acquire(id, new Core.OwnerToken(sid));
                if (ticket.Status == OperationTicketStatus.Accepted)
                {
                    return new API.OwnerSession(
                        sid,
                        id,
                        _resources,
                        _scheduler,
                        _events
                    );
                }
            }

            throw new InvalidOperationException("OWN2001: No resource available.");
        }
    }

    /// <summary>Dispose the host; equivalent to Shutdown().</summary>
    public void Dispose() => Shutdown();
}
