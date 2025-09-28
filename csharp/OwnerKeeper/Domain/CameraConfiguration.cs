namespace OwnerKeeper.Domain;

/// <summary>
/// Camera configuration value object. (SPECS §7)
/// </summary>
public sealed class CameraConfiguration
{
    /// <summary>Requested resolution. (SPECS §7)</summary>
    public CameraResolution Resolution { get; }

    /// <summary>Requested pixel format. (SPECS §7)</summary>
    public PixelFormat PixelFormat { get; }

    /// <summary>Requested frame rate (fps). (SPECS §7)</summary>
    public FrameRate FrameRate { get; }

    /// <summary>
    /// Create a camera configuration and validate values.
    /// Throws <see cref="System.ArgumentException"/> on invalid inputs (ARG3001).
    /// </summary>
    /// <param name="resolution">Resolution in pixels.</param>
    /// <param name="pixelFormat">Pixel format.</param>
    /// <param name="frameRate">Frame rate in fps.</param>
    public CameraConfiguration(
        CameraResolution resolution,
        PixelFormat pixelFormat,
        FrameRate frameRate
    )
    {
        Resolution = resolution;
        PixelFormat = pixelFormat;
        FrameRate = frameRate;
        ConfigurationValidator.Validate(this); // (REQ-CF-001) Validate on creation
    }
}

/// <summary>Resolution in pixels. Width/Height must be positive. (SPECS §7)</summary>
public readonly record struct CameraResolution(int Width, int Height)
{
    /// <summary>Returns a string like "1920x1080".</summary>
    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>Supported pixel formats (subset). (SPECS §7)</summary>
public enum PixelFormat
{
    /// <summary>24-bit RGB (8 bits per channel).</summary>
    Rgb24 = 1,

    /// <summary>Planar YUV 4:2:0.</summary>
    Yuv420 = 2,
}

/// <summary>Frames per second (integer). Must be positive. (SPECS §7)</summary>
public readonly record struct FrameRate(int Fps)
{
    /// <summary>Returns a string like "30fps".</summary>
    public override string ToString() => $"{Fps}fps";
}

/// <summary>
/// Validation helpers for configuration value objects.
/// Invalid values shall raise ArgumentException (ARG3001). (SPECS §7)
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Validate configuration values; throws ArgumentException on invalid values (ARG3001).
    /// </summary>
    /// <param name="config">Configuration to validate.</param>
    public static void Validate(CameraConfiguration config)
    {
        if (config.Resolution.Width <= 0 || config.Resolution.Height <= 0)
        {
            throw new System.ArgumentException(
                "ARG3001: Resolution must be positive."
            );
        }

        if (config.FrameRate.Fps <= 0)
        {
            throw new System.ArgumentException(
                "ARG3001: FrameRate must be positive."
            );
        }
    }
}
