using System;
using System.Collections.Generic;
using System.Globalization;
using BlockTranscoding.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace BlockTranscoding;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        ConfigurationChanged += OnConfigurationChanged;
    }

    /// <summary>
    /// Fired after configuration has been saved so the playback timer can be stopped or started
    /// </summary>
    public event EventHandler? BlockTranscodingChanged;

    /// <inheritdoc />
    public override string Name => "BlockTranscoding";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("55330139-1f8b-4e5d-a207-2afece96e7a6");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            },
        ];
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        BlockTranscodingChanged?.Invoke(this, EventArgs.Empty);
    }
}
