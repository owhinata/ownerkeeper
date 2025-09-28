using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Hardware;

/// <summary>
/// Simple stub implementation that simulates camera operations.
/// Uses ArrayPool to rent/return a small buffer on Start to mimic resource usage.
/// </summary>
public sealed class CameraStub : IHardwareResource
{
    private volatile bool _streaming; // (reserved) streaming flag for future checks
    private volatile bool _paused; // (reserved) pause state indicator for future checks
    private byte[]? _buffer;

    /// <summary>Simulate start operation with small delay and buffer rent.</summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);
        _buffer ??= ArrayPool<byte>.Shared.Rent(1024);
        _ = _streaming; // suppress until used elsewhere
        _streaming = true;
        _paused = false;
    }

    /// <summary>Simulate stop operation and return buffer.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);
        if (_buffer is not null && _streaming)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
        _streaming = false;
        _paused = false;
    }

    /// <summary>Simulate pause operation.</summary>
    public async Task PauseAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken);
        _ = _paused; // suppress analyzer until used in future phases
    }

    /// <summary>Simulate resume operation.</summary>
    public async Task ResumeAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken);
        _ = _paused; // suppress analyzer until used in future phases
    }

    /// <summary>Accept any configuration (stub).</summary>
    public Task UpdateConfigurationAsync(
        CameraConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        // Accept all configs in stub; no delay needed.
        return Task.CompletedTask;
    }
}

/// <summary>Default factory creating CameraStub instances.</summary>
/// <summary>Default hardware factory producing stub adapters.</summary>
public sealed class DefaultHardwareFactory : IHardwareResourceFactory
{
    /// <summary>Create a stub adapter for the provided id.</summary>
    public IHardwareResource Create(ResourceId id) => new CameraStub();
}
