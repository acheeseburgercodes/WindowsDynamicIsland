using DynamicIsland.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DynamicIsland.Services;

public sealed class MediaSessionService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _disposed;

    public event EventHandler<MediaSnapshot>? MediaChanged;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += OnCurrentSessionChanged;
        _manager.SessionsChanged += OnSessionsChanged;
        AttachSession(_manager.GetCurrentSession());
        await PublishSnapshotAsync();
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_session is null)
        {
            return;
        }

        var status = _session.GetPlaybackInfo()?.PlaybackStatus;
        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            await _session.TryPauseAsync();
        }
        else
        {
            await _session.TryPlayAsync();
        }
    }

    public Task PreviousAsync() => _session is null
        ? Task.CompletedTask
        : _session.TrySkipPreviousAsync().AsTask();

    public Task NextAsync() => _session is null
        ? Task.CompletedTask
        : _session.TrySkipNextAsync().AsTask();

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        AttachSession(sender.GetCurrentSession());
        _ = PublishSnapshotAsync();
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        AttachSession(sender.GetCurrentSession());
        _ = PublishSnapshotAsync();
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }

        _session = session;

        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        }
    }

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => _ = PublishSnapshotAsync();

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => _ = PublishSnapshotAsync();

    private async Task PublishSnapshotAsync()
    {
        var session = _session;
        if (session is null)
        {
            MediaChanged?.Invoke(this, MediaSnapshot.Empty);
            return;
        }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            byte[]? thumbnail = null;

            if (properties.Thumbnail is not null)
            {
                using var stream = await properties.Thumbnail.OpenReadAsync();
                if (stream.Size <= 8 * 1024 * 1024)
                {
                    using var reader = new DataReader(stream);
                    await reader.LoadAsync((uint)stream.Size);
                    thumbnail = new byte[(int)stream.Size];
                    reader.ReadBytes(thumbnail);
                }
            }

            var controls = playback?.Controls;
            MediaChanged?.Invoke(this, new MediaSnapshot(
                true,
                string.IsNullOrWhiteSpace(properties.Title) ? "Unknown title" : properties.Title,
                string.IsNullOrWhiteSpace(properties.Artist) ? "Unknown artist" : properties.Artist,
                properties.AlbumTitle ?? string.Empty,
                playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                controls?.IsPlayEnabled == true || controls?.IsPauseEnabled == true,
                controls?.IsPreviousEnabled == true,
                controls?.IsNextEnabled == true,
                thumbnail));
        }
        catch (Exception) when (!_disposed)
        {
            MediaChanged?.Invoke(this, MediaSnapshot.Empty);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        AttachSession(null);
    }
}
