using System.ComponentModel;
using MediaBrowser.Model.Plugins;

namespace BlockTranscoding.Configuration;

/// <summary>
/// Resolutions.
/// </summary>
public enum Resolutions
{
    /// <summary>
    /// 480p.
    /// </summary>
    [Description("480p")]
    StandardDefinition,

    /// <summary>
    /// 720p.
    /// </summary>
    [Description("720p")]
    HighDefinition,

    /// <summary>
    /// Full HD.
    /// </summary>
    [Description("1080p")]
    FullHD,

    /// <summary>
    /// Quad HD.
    /// </summary>
    [Description("1440p")]
    QuadHD,

    /// <summary>
    /// 4K.
    /// </summary>
    [Description("2160p")]
    UltraHD,
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin should start blocking the playback.
    /// </summary>
    public bool BlockTranscoding { get; set; }

    /// <summary>
    /// Gets or sets a custom message when the playback gets stopped.
    /// </summary>
    public string CustomMessage { get; set; } = "4k trasconding is disabled.";

    /// <summary>
    /// Gets or sets the max allowed playback resolution.
    /// </summary>
    public Resolutions MaxResolution { get; set; } = Resolutions.FullHD;
}
