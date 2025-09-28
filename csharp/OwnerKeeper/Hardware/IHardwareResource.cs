using System.Threading;
using System.Threading.Tasks;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Hardware;

/// <summary>
/// Abstraction for a hardware resource capable of camera operations.
/// (SPECS §3.2)
/// </summary>
/// <summary>Hardware resource contract.</summary>
public interface IHardwareResource
{
    /// <summary>Start streaming.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stop streaming.</summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>Pause streaming.</summary>
    Task PauseAsync(CancellationToken cancellationToken);

    /// <summary>Resume streaming.</summary>
    Task ResumeAsync(CancellationToken cancellationToken);

    /// <summary>Apply configuration changes.</summary>
    Task UpdateConfigurationAsync(
        CameraConfiguration configuration,
        CancellationToken cancellationToken
    );
}

/// <summary>Factory for creating hardware resource adapters.</summary>
/// <summary>Factory to construct IHardwareResource per ResourceId.</summary>
public interface IHardwareResourceFactory
{
    /// <summary>Create a hardware adapter for the given resource.</summary>
    IHardwareResource Create(ResourceId id);
}
