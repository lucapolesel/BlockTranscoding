using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using BlockTranscoding.Utilities;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace BlockTranscoding;

/// <summary>
/// Automatically block content from transcoding.
/// </summary>
public class BlockTranscoding : IServerEntryPoint, IDisposable
{
    private readonly object _playbackStoppedCommandLock = new();

    private readonly IUserDataManager _userDataManager;
    private readonly ISessionManager _sessionManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BlockTranscoding> _logger;

    private readonly System.Timers.Timer _playbackTimer = new(1000);

    private readonly Dictionary<string, bool> _playbackStoppedCommand;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockTranscoding"/> class.
    /// </summary>
    /// <param name="userDataManager">User data manager.</param>
    /// <param name="sessionManager">Session manager.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public BlockTranscoding(
        IUserDataManager userDataManager,
        ISessionManager sessionManager,
        ILoggerFactory loggerFactory)
    {
        _userDataManager = userDataManager;
        _sessionManager = sessionManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BlockTranscoding>();
        _playbackStoppedCommand = new Dictionary<string, bool>();
    }

    /// <summary>
    /// Subscribe to the PlaybackStart callback.
    /// </summary>
    /// <returns>Task completion.</returns>
    public Task RunAsync()
    {
        _logger.LogInformation("Setting up BlockTranscoding");

        _userDataManager.UserDataSaved += UserDataManager_UserDataSaved;
        Plugin.Instance!.BlockTranscodingChanged += BlockTranscodingChanged;

        _playbackTimer.AutoReset = true;
        _playbackTimer.Elapsed += PlaybackTimer_Elapsed;

        BlockTranscodingChanged(null, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private void BlockTranscodingChanged(object? sender, EventArgs e)
    {
        var newState = Plugin.Instance!.Configuration.BlockTranscoding;

        _logger.LogDebug("Setting playback timer enabled to {NewState}.", newState);

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
            foreach (var needle in _sessionManager.Sessions)
            {
                if (needle.UserId == e.UserId && needle.NowPlayingItem?.Id == itemId)
                {
                    session = needle;
                    break;
                }
            }

            if (session == null)
            {
                _logger.LogInformation("Unable to find session for {Item}", itemId);
                return;
            }
        }
        catch (Exception ex) when (ex is NullReferenceException || ex is ResourceNotFoundException)
        {
            return;
        }

        // Reset the stop command state for this device.
        lock (_playbackStoppedCommandLock)
        {
            var device = session.DeviceId;

            _logger.LogDebug("Resetting seek command state for session {Session}", device);
            _playbackStoppedCommand[device] = false;
        }
    }

    private void PlaybackTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        foreach (var session in _sessionManager.Sessions)
        {
            var deviceId = session.DeviceId;
            var playingItem = session.NowPlayingItem;

            lock (_playbackStoppedCommandLock)
            {
                if (_playbackStoppedCommand.TryGetValue(deviceId, out var stopped) && stopped)
                {
                    _logger.LogTrace("Already sent stop command for session {Session}", deviceId);
                    continue;
                }
            }

            // Check if it is actually a video
            if (playingItem?.MediaType == "Video")
            {
                // Check if it is transcoding
                if (session.PlayState.PlayMethod == PlayMethod.Transcode)
                {
                    // Ignore if the video is not being transcoded
                    if (session.TranscodingInfo.IsVideoDirect)
                    {
                        continue;
                    }

                    // Check if the video that is being transcoded is over the max allowed resolution
                    var maxRes = Plugin.Instance!.Configuration.MaxResolution;
                    var maxResSize = ResolutionUtility.GetSize(maxRes);

                    if (playingItem.Width > maxResSize.Width || playingItem.Height > maxResSize.Height)
                    {
                        _sessionManager.SendPlaystateCommand(
                            session.Id,
                            session.Id,
                            new PlaystateRequest
                            {
                                Command = PlaystateCommand.Stop,
                                ControllingUserId = session.UserId.ToString("N"),
                            },
                            CancellationToken.None);

                        lock (_playbackStoppedCommandLock)
                        {
                            _logger.LogTrace("Setting stop command state for session {Session}", deviceId);
                            _playbackStoppedCommand[deviceId] = true;
                        }

                        var customMessage = Plugin.Instance!.Configuration.CustomMessage;

                        if (string.IsNullOrEmpty(customMessage))
                        {
                            continue;
                        }

                        // TODO: Maybe allow the admin to tell the user which resolution has been blocked.

                        _sessionManager.SendMessageCommand(
                            session.Id,
                            session.Id,
                            new MessageCommand()
                            {
                                Header = string.Empty,
                                Text = customMessage,
                                TimeoutMs = 2000,
                            },
                            CancellationToken.None);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose.
    /// </summary>
    /// <param name="disposing">Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _userDataManager.UserDataSaved -= UserDataManager_UserDataSaved;

        _playbackTimer?.Stop();
        _playbackTimer?.Dispose();
    }
}
