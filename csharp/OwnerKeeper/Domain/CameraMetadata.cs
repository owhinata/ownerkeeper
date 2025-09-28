namespace OwnerKeeper.Domain;

/// <summary>
/// Metadata of a camera stream delivered on success events.
/// (SPECS §4.3) For Phase 4, this mirrors configuration.
/// </summary>
public sealed class CameraMetadata
{
    /// <summary>Effective resolution.</summary>
    public CameraResolution Resolution { get; }

    /// <summary>Effective pixel format.</summary>
    public PixelFormat PixelFormat { get; }

    /// <summary>Effective frame rate (fps).</summary>
    public FrameRate FrameRate { get; }

    /// <summary>Create metadata from effective stream parameters.</summary>
    public CameraMetadata(
        CameraResolution resolution,
        PixelFormat pixelFormat,
        FrameRate frameRate
    )
    {
        Resolution = resolution;
        PixelFormat = pixelFormat;
        FrameRate = frameRate;
    }
}
