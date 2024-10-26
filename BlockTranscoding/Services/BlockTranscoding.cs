using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using BlockTranscoding.Utilities;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace BlockTranscoding.Services;

/// <summary>
///     Automatically block content from transcoding.
/// </summary>
public class BlockTranscoding(
    IUserDataManager userDataManager,
    ISessionManager sessionManager,
    ILogger<BlockTranscoding> logger) : IHostedService, IDisposable
{
    private readonly Dictionary<string, bool> _playbackStoppedCommand = new();
    private readonly object _playbackStoppedCommandLock = new();

    private readonly Timer _playbackTimer = new(1000);

    /// <summary>
    ///     Dispose.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Setting up {nameof(BlockTranscoding)}");

        userDataManager.UserDataSaved += UserDataManager_UserDataSaved;
        Plugin.Instance!.BlockTranscodingChanged += BlockTranscodingChanged;

        _playbackTimer.AutoReset = true;
        _playbackTimer.Elapsed += PlaybackTimer_Elapsed;

        BlockTranscodingChanged(null, EventArgs.Empty);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        userDataManager.UserDataSaved -= UserDataManager_UserDataSaved;

        return Task.CompletedTask;
    }

    private void BlockTranscodingChanged(object? sender, EventArgs e)
    {
        var newState = Plugin.Instance!.Configuration.BlockTranscoding;

        logger.LogDebug("Setting playback timer enabled to {NewState}.", newState);

        _playbackTimer.Enabled = newState;
    }

    private void UserDataManager_UserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        var itemId = e.Item.Id;

        if (e.SaveReason != UserDataSaveReason.PlaybackStart && e.SaveReason != UserDataSaveReason.PlaybackFinished)
        {
            return;
        }

        // Lookup the session for this item.
        SessionInfo? session = null;

        try
        {
            foreach (var needle in sessionManager.Sessions)
            {
                if (needle.UserId == e.UserId && needle.NowPlayingItem?.Id == itemId)
                {
                    session = needle;
                    break;
                }
            }

            if (session == null)
            {
                logger.LogInformation("Unable to find session for {Item}", itemId);
                return;
            }
        }
        catch (Exception ex) when (ex is NullReferenceException or ResourceNotFoundException)
        {
            return;
        }

        // Reset the stop command state for this device.
        lock (_playbackStoppedCommandLock)
        {
            var device = session.DeviceId;

            logger.LogDebug("Resetting seek command state for session {Session}", device);

            _playbackStoppedCommand[device] = false;
        }
    }

    private void PlaybackTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        foreach (var session in sessionManager.Sessions)
        {
            var deviceId = session.DeviceId;
            var playingItem = session.NowPlayingItem;

            lock (_playbackStoppedCommandLock)
            {
                if (_playbackStoppedCommand.TryGetValue(deviceId, out var stopped) && stopped)
                {
                    logger.LogTrace("Already sent stop command for session {Session}", deviceId);
                    continue;
                }
            }

            // Check if it is actually a video
            if (playingItem?.MediaType != MediaType.Video)
            {
                continue;
            }

            // Check if it is transcoding
            if (session.PlayState.PlayMethod != PlayMethod.Transcode)
            {
                continue;
            }

            // Ignore if the video is not being transcoded
            if (session.TranscodingInfo.IsVideoDirect)
            {
                continue;
            }

            // Check if the video that is being transcoded is over the max allowed resolution
            var maxRes = Plugin.Instance!.Configuration.MaxResolution;
            var maxResSize = ResolutionUtility.GetSize(maxRes);

            if (!(playingItem.Width > maxResSize.Width) && !(playingItem.Height > maxResSize.Height))
            {
                continue;
            }

            sessionManager.SendPlaystateCommand(
                session.Id,
                session.Id,
                new PlaystateRequest { Command = PlaystateCommand.Stop, ControllingUserId = session.UserId.ToString("N") },
                CancellationToken.None);

            lock (_playbackStoppedCommandLock)
            {
                logger.LogTrace("Setting stop command state for session {Session}", deviceId);
                _playbackStoppedCommand[deviceId] = true;
            }

            var customMessage = Plugin.Instance!.Configuration.CustomMessage;

            if (string.IsNullOrEmpty(customMessage))
            {
                continue;
            }

            // TODO: Maybe allow the admin to tell the user which resolution has been blocked.

            sessionManager.SendMessageCommand(
                session.Id,
                session.Id,
                new MessageCommand { Header = string.Empty, Text = customMessage, TimeoutMs = 2000 },
                CancellationToken.None);
        }
    }

    /// <summary>
    ///     Protected dispose.
    /// </summary>
    /// <param name="disposing">Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _playbackTimer.Stop();
        _playbackTimer.Dispose();
    }
}
