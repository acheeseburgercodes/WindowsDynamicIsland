using DynamicIsland.Core;
using DynamicIsland.Models;
using DynamicIsland.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace DynamicIsland;

public sealed partial class MainWindow : Window
{
    private const int CollapsedWidth = 188;
    private const int CollapsedHeight = 48;
    private const int ExpandedWidth = 590;
    private const int ExpandedHeight = 210;

    private readonly IslandManager _islandManager = new();
    private readonly MediaSessionService _mediaService = new();
    private readonly WindowService _windowService;
    private readonly DispatcherTimer _resizeTimer = new() { Interval = TimeSpan.FromMilliseconds(12) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };

    private DateTimeOffset _animationStarted;
    private int _fromWidth;
    private int _fromHeight;
    private int _toWidth = CollapsedWidth;
    private int _toHeight = CollapsedHeight;
    private int _currentWidth = CollapsedWidth;
    private int _currentHeight = CollapsedHeight;

    public MainWindow()
    {
        InitializeComponent();
        _windowService = new WindowService(this);
        _windowService.Configure(CollapsedWidth, CollapsedHeight);

        _islandManager.StateChanged += OnIslandStateChanged;
        _mediaService.MediaChanged += OnMediaChanged;
        _resizeTimer.Tick += ResizeTimer_Tick;
        _collapseTimer.Tick += CollapseTimer_Tick;
        Closed += OnClosed;
        Activated += OnFirstActivated;
    }

    private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        try
        {
            await _mediaService.InitializeAsync();
        }
        catch (Exception)
        {
            ApplyMediaSnapshot(MediaSnapshot.Empty);
        }
    }

    private void Island_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _collapseTimer.Stop();
        _islandManager.TransitionTo(IslandState.Expanded);
    }

    private void Island_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_islandManager.State == IslandState.Expanded)
        {
            _collapseTimer.Start();
        }
    }

    private void Island_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_islandManager.State == IslandState.Collapsed)
        {
            _islandManager.TransitionTo(IslandState.Expanded);
        }
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        _collapseTimer.Stop();
        _islandManager.TransitionTo(IslandState.Collapsed);
    }

    private void CollapseTimer_Tick(object? sender, object e)
    {
        _collapseTimer.Stop();
        _islandManager.TransitionTo(IslandState.Collapsed);
    }

    private void OnIslandStateChanged(object? sender, IslandState state)
    {
        var expanded = state != IslandState.Collapsed;
        StartResizeAnimation(
            expanded ? ExpandedWidth : CollapsedWidth,
            expanded ? ExpandedHeight : CollapsedHeight);
        AnimateContent(expanded);
    }

    private void StartResizeAnimation(int width, int height)
    {
        _fromWidth = _currentWidth;
        _fromHeight = _currentHeight;
        _toWidth = width;
        _toHeight = height;
        _animationStarted = DateTimeOffset.UtcNow;
        _resizeTimer.Start();
    }

    private void ResizeTimer_Tick(object? sender, object e)
    {
        const double durationMs = 240;
        var elapsed = (DateTimeOffset.UtcNow - _animationStarted).TotalMilliseconds;
        var progress = Math.Clamp(elapsed / durationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);

        _currentWidth = (int)Math.Round(_fromWidth + ((_toWidth - _fromWidth) * eased));
        _currentHeight = (int)Math.Round(_fromHeight + ((_toHeight - _fromHeight) * eased));
        _windowService.ResizeAndCenter(_currentWidth, _currentHeight);

        var expandedFraction = (_currentWidth - CollapsedWidth) /
            (double)(ExpandedWidth - CollapsedWidth);
        IslandSurface.CornerRadius = new CornerRadius(22 + (10 * expandedFraction));

        if (progress >= 1)
        {
            _resizeTimer.Stop();
        }
    }

    private void AnimateContent(bool expanded)
    {
        var show = expanded ? ExpandedContent : CollapsedContent;
        var hide = expanded ? CollapsedContent : ExpandedContent;
        show.Visibility = Visibility.Visible;

        AnimateVisual(hide, 1, 0, 1, 0.96f, 90);
        AnimateVisual(show, 0, 1, 0.96f, 1, 190);

        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            hide.Visibility = Visibility.Collapsed;
        };
        hideTimer.Start();
    }

    private static void AnimateVisual(
        UIElement element,
        float fromOpacity,
        float toOpacity,
        float fromScale,
        float toScale,
        int durationMs)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new System.Numerics.Vector3(
            (float)element.ActualSize.X / 2,
            (float)element.ActualSize.Y / 2,
            0);

        var compositor = visual.Compositor;
        var easing = compositor.CreateCubicBezierEasingFunction(
            new System.Numerics.Vector2(0.2f, 0.8f),
            new System.Numerics.Vector2(0.2f, 1));

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, fromOpacity);
        opacity.InsertKeyFrame(1, toOpacity, easing);
        opacity.Duration = TimeSpan.FromMilliseconds(durationMs);

        var scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new System.Numerics.Vector3(fromScale, fromScale, 1));
        scale.InsertKeyFrame(1, new System.Numerics.Vector3(toScale, toScale, 1), easing);
        scale.Duration = TimeSpan.FromMilliseconds(durationMs);

        visual.StartAnimation(nameof(Visual.Opacity), opacity);
        visual.StartAnimation(nameof(Visual.Scale), scale);
    }

    private void OnMediaChanged(object? sender, MediaSnapshot snapshot)
    {
        DispatcherQueue.TryEnqueue(() => ApplyMediaSnapshot(snapshot));
    }

    private async void ApplyMediaSnapshot(MediaSnapshot snapshot)
    {
        TrackTitle.Text = snapshot.Title;
        TrackArtist.Text = snapshot.Artist;
        AlbumTitle.Text = snapshot.Album;
        AlbumTitle.Visibility = string.IsNullOrWhiteSpace(snapshot.Album)
            ? Visibility.Collapsed
            : Visibility.Visible;
        CollapsedTitle.Text = snapshot.HasSession ? snapshot.Title : "Dynamic Island";
        CollapsedArtist.Text = snapshot.HasSession ? snapshot.Artist : "Hover to open";
        PlayPauseIcon.Glyph = snapshot.IsPlaying ? "\uE769" : "\uE768";
        PlayPauseButton.IsEnabled = snapshot.CanPlayPause;
        PreviousButton.IsEnabled = snapshot.CanPrevious;
        NextButton.IsEnabled = snapshot.CanNext;

        var image = await CreateImageAsync(snapshot.Thumbnail);
        Artwork.Source = image;
        CollapsedArtwork.Source = image;
        ArtworkFallbackIcon.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
        CollapsedFallbackIcon.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static async Task<BitmapImage?> CreateImageAsync(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }

    private async void PreviousButton_Click(object sender, RoutedEventArgs e) =>
        await _mediaService.PreviousAsync();

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e) =>
        await _mediaService.TogglePlayPauseAsync();

    private async void NextButton_Click(object sender, RoutedEventArgs e) =>
        await _mediaService.NextAsync();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _resizeTimer.Stop();
        _collapseTimer.Stop();
        _mediaService.Dispose();
    }
}
