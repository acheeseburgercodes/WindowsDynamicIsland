using System.Runtime.InteropServices;
using DynamicIsland.Models;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland.Services;

/// <summary>
/// Reads Windows toast notifications only after the user grants the packaged app access.
/// Notification content is kept in memory and is never persisted by this service.
/// </summary>
public sealed class SystemNotificationService : IDisposable
{
    private const int ErrorInsufficientBuffer = 122;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly HashSet<uint> _knownIds = [];
    private UserNotificationListener? _listener;
    private bool _subscribed;

    public event EventHandler<NotificationPreview>? NotificationReceived;

    public bool IsListening => _subscribed;
    public bool IsSupported => HasPackageIdentity();

    public async Task<string> StartAsync(bool requestAccess)
    {
        if (!HasPackageIdentity())
            return "System notifications require an MSIX build with notification-listener capability.";

        try
        {
            _listener ??= UserNotificationListener.Current;
            var access = requestAccess
                ? await _listener.RequestAccessAsync()
                : _listener.GetAccessStatus();
            if (access != UserNotificationListenerAccessStatus.Allowed)
                return access == UserNotificationListenerAccessStatus.Denied
                    ? "Notification access is off. Allow it in Windows settings to use this feature."
                    : "Allow notification access when Windows asks, then try again.";

            if (_subscribed) return "Windows notifications are active.";

            // Existing Action Center items are not new arrivals and should not animate on launch.
            var current = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            _knownIds.Clear();
            foreach (var notification in current)
                _knownIds.Add(notification.Id);
            _listener.NotificationChanged += OnNotificationChanged;
            _subscribed = true;
            return "Windows notifications are active.";
        }
        catch (Exception exception)
        {
            return $"Windows notification access is unavailable: {exception.Message}";
        }
    }

    private async void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        await _syncLock.WaitAsync();
        try
        {
            if (sender.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
                return;

            var current = await sender.GetNotificationsAsync(NotificationKinds.Toast);
            var currentIds = current.Select(notification => notification.Id).ToHashSet();
            foreach (var notification in current)
            {
                if (!_knownIds.Add(notification.Id)) continue;
                try
                {
                    var preview = ReadPreview(notification);
                    if (preview is not null)
                        NotificationReceived?.Invoke(this, preview);
                }
                catch
                {
                    // One malformed notification must not interrupt other apps' alerts.
                }
            }
            _knownIds.IntersectWith(currentIds);
        }
        catch
        {
            // Permission can be revoked or the notification service can restart at any time.
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private static NotificationPreview? ReadPreview(UserNotification notification)
    {
        var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
        var text = binding?.GetTextElements().Select(element => element.Text?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? [];
        var app = notification.AppInfo.DisplayInfo.DisplayName;
        var title = text.FirstOrDefault() ?? app;
        var body = string.Join(" · ", text.Skip(1));
        if (string.IsNullOrWhiteSpace(title)) return null;
        return new NotificationPreview(Clip(app, 40), Clip(title, 90), Clip(body, 150));
    }

    private static string Clip(string? value, int limit)
    {
        var result = value?.Trim() ?? string.Empty;
        return result.Length <= limit ? result : result[..(limit - 1)] + "…";
    }

    private static bool HasPackageIdentity()
    {
        uint length = 0;
        var result = GetCurrentPackageFullName(ref length, 0);
        return result is 0 or ErrorInsufficientBuffer;
    }

    public void Dispose()
    {
        if (_subscribed && _listener is not null)
            _listener.NotificationChanged -= OnNotificationChanged;
        _subscribed = false;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, nint packageFullName);
}
