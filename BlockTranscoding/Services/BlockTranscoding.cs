using System.Threading;
using System.Threading.Tasks;
using BlockTranscoding.Utilities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlockTranscoding.Services;

/// <summary>
/// Automatically block content from transcoding.
/// </summary>
public sealed class BlockTranscoding(
    ILogger<BlockTranscoding> logger,
    ISessionManager sessionManager) : IHostedService
{
    /// <summary>
    /// Subscribe to the PlaybackProgress callback.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task completion.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Setting up {nameof(BlockTranscoding)}..");

        sessionManager.PlaybackProgress += OnPlaybackProgress;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Unsubscribes from the PlaybackProgress callback.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task completion.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        sessionManager.PlaybackProgress -= OnPlaybackProgress;

        return Task.CompletedTask;
    }

    private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
    {
        // Check if we actually have to block any transcoding
        if (!Plugin.Instance!.Configuration.BlockTranscoding)
        {
            return;
        }

        var sessionId = e.Session.Id;
        var sessionUserId = e.Session.UserId;
        var playingItem = e.Session.NowPlayingItem;
        var playState = e.Session.PlayState;

        // We only want to block video type
        if (playingItem?.MediaType is not MediaType.Video)
        {
            return;
        }

        // Check if it is actually transcoding
        if (playState?.PlayMethod is not PlayMethod.Transcode)
        {
            return;
        }

        // Check if the video that is being transcoded is over the max allowed resolution
        var maxRes = Plugin.Instance.Configuration.MaxResolution;
        var maxResSize = ResolutionUtility.GetSize(maxRes);

        // Make sure that the resolution does match
        if (!(playingItem.Width > maxResSize.Width) && !(playingItem.Height > maxResSize.Height))
        {
            return;
        }

        // Send the stop command
        sessionManager.SendPlaystateCommand(
            sessionId,
            sessionId,
            new PlaystateRequest { Command = PlaystateCommand.Stop, ControllingUserId = sessionUserId.ToString("N") },
            CancellationToken.None);

        var customMessage = Plugin.Instance.Configuration.CustomMessage;

        if (string.IsNullOrEmpty(customMessage))
        {
            return;
        }

        // TODO: Maybe allow the admin to tell the user which resolution has been blocked.

        // Display a custom message after the video has been stopped
        sessionManager.SendMessageCommand(
            sessionId,
            sessionId,
            new MessageCommand { Header = string.Empty, Text = customMessage, TimeoutMs = 2000 },
            CancellationToken.None);
    }
}
