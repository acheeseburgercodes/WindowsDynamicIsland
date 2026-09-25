using System.Collections.ObjectModel;
using System.Diagnostics;
using DynamicIsland.Core;
using DynamicIsland.Models;
using DynamicIsland.Services;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace DynamicIsland;

public sealed partial class MainWindow : Window
{
    private const int CollapsedWidth = 188;
    private const int CollapsedHeight = 48;
    private const int ExpandedWidth = 650;
    private const int ExpandedHeight = 410;

    private readonly IslandManager _islandManager = new();
    private readonly MediaSessionService _mediaService = new();
    private readonly ConfigurationService _configurationService = new();
    private readonly ClipboardService _clipboardService = new();
    private readonly ObservableCollection<DockedFileItem> _files = [];
    private readonly WindowService _windowService;
    private readonly DispatcherTimer _resizeTimer = new() { Interval = TimeSpan.FromMilliseconds(12) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private readonly DispatcherTimer _settingsSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };

    private AppConfiguration _configuration = new();
    private DateTimeOffset _animationStarted;
    private bool _settingsLoaded;
    private int _fromWidth;
    private int _fromHeight;
    private int _toWidth = CollapsedWidth;
    private int _toHeight = CollapsedHeight;
    private int _currentWidth = CollapsedWidth;
    private int _currentHeight = CollapsedHeight;

    public MainWindow()
    {
        InitializeComponent();
        FileList.ItemsSource = _files;

        _windowService = new WindowService(this);
        _windowService.Configure(CollapsedWidth, CollapsedHeight);

        _islandManager.StateChanged += OnIslandStateChanged;
        _mediaService.MediaChanged += OnMediaChanged;
        _resizeTimer.Tick += ResizeTimer_Tick;
        _collapseTimer.Tick += CollapseTimer_Tick;
        _settingsSaveTimer.Tick += SettingsSaveTimer_Tick;
        Closed += OnClosed;
        Activated += OnFirstActivated;
    }

    private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;

        _configuration = await _configurationService.LoadAsync();
        _configuration.SurfaceOpacity = Math.Clamp(_configuration.SurfaceOpacity, 0.35, 1);
        foreach (var file in _configuration.DockedFiles.Where(file => File.Exists(file.FullPath)))
        {
            _files.Add(file);
        }

        OpacitySlider.Value = _configuration.SurfaceOpacity * 100;
        ApplySurfaceOpacity(_configuration.SurfaceOpacity);
        _settingsLoaded = true;

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
        if (_islandManager.State == IslandState.Expanded && MediaPanel.Visibility == Visibility.Visible)
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

    private void MediaNavButton_Click(object sender, RoutedEventArgs e) => ShowPanel(MediaPanel);

    private void FilesNavButton_Click(object sender, RoutedEventArgs e) => ShowPanel(FilesPanel);

    private async void ClipboardNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(ClipboardPanel);
        await PasteClipboardTextAsync(showEmptyMessage: false);
    }

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e) => ShowPanel(SettingsPanel);

    private void ShowPanel(UIElement panel)
    {
        _collapseTimer.Stop();
        MediaPanel.Visibility = panel == MediaPanel ? Visibility.Visible : Visibility.Collapsed;
        FilesPanel.Visibility = panel == FilesPanel ? Visibility.Visible : Visibility.Collapsed;
        ClipboardPanel.Visibility = panel == ClipboardPanel ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = panel == SettingsPanel ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMediaChanged(object? sender, MediaSnapshot snapshot) =>
        DispatcherQueue.TryEnqueue(() => ApplyMediaSnapshot(snapshot));

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

    private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var files = await picker.PickMultipleFilesAsync();
        await AddFilesAsync(files);
    }

    private void FilesDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Add to file shelf";
            e.DragUIOverride.IsGlyphVisible = true;
        }
    }

    private async void FilesDropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        await AddFilesAsync(items.OfType<StorageFile>());
    }

    private async void PasteFilesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await AddFilesAsync(await _clipboardService.GetFilesAsync());
        }
        catch (Exception)
        {
            ClipboardStatus.Text = "The clipboard could not be read.";
        }
    }

    private async Task AddFilesAsync(IEnumerable<StorageFile> files)
    {
        var changed = false;
        foreach (var file in files)
        {
            if (_files.Any(item => string.Equals(item.FullPath, file.Path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _files.Add(new DockedFileItem
            {
                Name = file.Name,
                FullPath = file.Path,
                Extension = file.FileType
            });
            changed = true;
        }

        if (changed)
        {
            await SaveConfigurationAsync();
        }
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string path || !File.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async void CopyFileButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string path || !File.Exists(path))
        {
            return;
        }

        await _clipboardService.SetFileAsync(path);
    }

    private async void RemoveFileButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string path)
        {
            return;
        }

        var file = _files.FirstOrDefault(item =>
            string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase));
        if (file is not null)
        {
            _files.Remove(file);
            await SaveConfigurationAsync();
        }
    }

    private async void PasteTextButton_Click(object sender, RoutedEventArgs e) =>
        await PasteClipboardTextAsync(showEmptyMessage: true);

    private async Task PasteClipboardTextAsync(bool showEmptyMessage)
    {
        try
        {
            var text = await _clipboardService.GetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                ClipboardTextBox.Text = text;
                ClipboardStatus.Text = "Pasted from Windows clipboard";
            }
            else if (showEmptyMessage)
            {
                ClipboardStatus.Text = "No text is currently on the clipboard";
            }
        }
        catch (Exception)
        {
            ClipboardStatus.Text = "The clipboard could not be read";
        }
    }

    private void CopyTextButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _clipboardService.SetText(ClipboardTextBox.Text);
            ClipboardStatus.Text = "Copied to Windows clipboard";
        }
        catch (Exception)
        {
            ClipboardStatus.Text = "The clipboard could not be updated";
        }
    }

    private void OpacitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var opacity = Math.Clamp(e.NewValue / 100, 0.35, 1);
        ApplySurfaceOpacity(opacity);

        if (!_settingsLoaded)
        {
            return;
        }

        _configuration.SurfaceOpacity = opacity;
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void ApplySurfaceOpacity(double opacity)
    {
        var alpha = (byte)Math.Round(255 * opacity);
        IslandSurface.Background = new SolidColorBrush(ColorHelper.FromArgb(alpha, 17, 17, 19));
        OpacityValueText.Text = $"{opacity:P0}";
    }

    private async void SettingsSaveTimer_Tick(object? sender, object e)
    {
        _settingsSaveTimer.Stop();
        await SaveConfigurationAsync();
    }

    private async Task SaveConfigurationAsync()
    {
        _configuration.DockedFiles = _files.ToList();
        await _configurationService.SaveAsync(_configuration);
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
        _settingsSaveTimer.Stop();
        _mediaService.Dispose();
    }
}
