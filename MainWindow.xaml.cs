using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DynamicIsland.Core;
using DynamicIsland.Models;
using DynamicIsland.Services;
using Microsoft.Win32;

namespace DynamicIsland;

public partial class MainWindow : Window
{
    private const double CollapsedWidth = 72;
    private const double CollapsedHeight = 26;
    private const double PreviewWidth = 430;
    private const double PreviewHeight = 58;
    private const double ExpandedWidth = 740;
    private const double ExpandedHeight = 520;

    private readonly IslandManager _islandManager = new();
    private readonly MediaSessionService _mediaService = new();
    private readonly BitmapImage _brandLogo = new(new Uri("pack://application:,,,/Assets/island-logo.png", UriKind.Absolute));
    private readonly ConfigurationService _configurationService = new();
    private readonly FileDockStorageService _fileDockStorage = new();
    private readonly AppCatalogService _appCatalog = new();
    private readonly InteractiveAppPanelService _interactiveAppPanel = new();
    private readonly VirtualDesktopService _virtualDesktopService = new();
    private readonly ObservableCollection<DockedFileItem> _files = [];
    private readonly ObservableCollection<AppLauncherItem> _applications = [];
    private readonly ObservableCollection<RunningApplicationInfo> _runningApplications = [];
    private readonly ObservableCollection<InstalledApplicationInfo> _installedApplications = [];
    private readonly WindowService _windowService;
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private readonly DispatcherTimer _settingsSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };

    private AppConfiguration _configuration = new();
    private bool _settingsLoaded;
    private bool _isDragging;
    private bool _isFileDragInProgress;
    private DockedFileItem? _fileDragItem;
    private BitmapSource? _currentArtwork;
    private MediaSnapshot _currentMediaSnapshot = MediaSnapshot.Empty;
    private Point _fileDragStart;
    private int _resizeAnimationVersion;
    private bool _installedAppsLoaded;
    private bool _isAnimating;
    private bool _isPinnedToAllDesktops;
    private int _desktopPinAttempts;
    private readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public MainWindow()
    {
        InitializeComponent();
        FileList.ItemsSource = _files;
        AppList.ItemsSource = _applications;
        RunningAppsComboBox.ItemsSource = _runningApplications;
        InstalledAppsComboBox.ItemsSource = _installedApplications;
        IslandSurface.RenderTransformOrigin = new Point(0.5, 0);
        IslandSurface.RenderTransform = new ScaleTransform(1, 1);
        LocationChanged += (_, _) => UpdateInteractivePanel();
        SizeChanged += (_, _) => UpdateInteractivePanel();
        AppHostViewport.SizeChanged += (_, _) => UpdateInteractivePanel();

        _windowService = new WindowService(this);
        _windowService.Configure();
        _islandManager.StateChanged += OnIslandStateChanged;
        _mediaService.MediaChanged += OnMediaChanged;
        _collapseTimer.Tick += CollapseTimer_Tick;
        _settingsSaveTimer.Tick += SettingsSaveTimer_Tick;
        _topmostTimer.Tick += (_, _) =>
        {
            if (!IsVisible || WindowState == WindowState.Minimized) return;
            if (!_isPinnedToAllDesktops && _desktopPinAttempts++ < 20)
            {
                _isPinnedToAllDesktops = _virtualDesktopService.TryPinIslandToAllDesktops();
                if (!_isPinnedToAllDesktops && _desktopPinAttempts == 20)
                    IslandSurface.ToolTip = "Windows blocked all-desktop pinning; the island may remain on one desktop.";
            }
            _windowService.EnsureTopmost();
            if (!_isAnimating && _islandManager.State == IslandState.Expanded &&
                AppsPanel.Visibility == Visibility.Visible)
                _interactiveAppPanel.UpdateBounds(AppHostViewport);
        };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized) _interactiveAppPanel.Suspend();
            else UpdateInteractivePanel();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _windowService.RemoveNativeBorder();
        _isPinnedToAllDesktops = _virtualDesktopService.TryPinIslandToAllDesktops();
        _topmostTimer.Start();
        _configuration = await _configurationService.LoadAsync();
        if (_configuration.ConfigurationVersion < 4)
        {
            _configuration.ConfigurationVersion = 4;
            _configuration.SurfaceOpacity = 0.76;
        }
        if (_configuration.ConfigurationVersion < 5)
        {
            _configuration.ConfigurationVersion = 5;
            _configuration.LogoStyle = IslandLogoStyle.Monogram;
        }
        if (_configuration.ConfigurationVersion < 6)
        {
            _configuration.ConfigurationVersion = 6;
        }
        if (_configuration.ConfigurationVersion < 7)
        {
            _configuration.ConfigurationVersion = 7;
            _configuration.Theme = IslandTheme.Dark;
        }

        _configuration.SurfaceOpacity = Math.Clamp(_configuration.SurfaceOpacity, 0.1, 1);
        foreach (var file in _configuration.DockedFiles)
        {
            if (file.IsManagedCopy && _fileDockStorage.IsManagedPath(file.FullPath) && File.Exists(file.FullPath))
            {
                _files.Add(file);
                continue;
            }

            var sourcePath = !string.IsNullOrWhiteSpace(file.OriginalPath) ? file.OriginalPath : file.FullPath;
            try
            {
                var stored = await _fileDockStorage.StoreAsync(sourcePath);
                if (stored is not null)
                {
                    _files.Add(stored);
                }
            }
            catch
            {
                // A missing or locked legacy source is skipped during one-time migration.
            }
        }
        foreach (var application in _configuration.Applications.Where(item => File.Exists(item.ExecutablePath)))
        {
            _applications.Add(application);
        }

        _windowService.Edge = _configuration.Edge;
        _windowService.Alignment = _configuration.Alignment;
        _windowService.HorizontalOffset = _configuration.HorizontalOffset;
        _windowService.VerticalOffset = _configuration.VerticalOffset;

        OpacitySlider.Value = _configuration.SurfaceOpacity * 100;
        EdgeComboBox.SelectedIndex = (int)_configuration.Edge;
        AlignmentComboBox.SelectedIndex = (int)_configuration.Alignment;
        HorizontalOffsetSlider.Value = _configuration.HorizontalOffset;
        VerticalOffsetSlider.Value = _configuration.VerticalOffset;
        LogoStyleComboBox.SelectedIndex = (int)_configuration.LogoStyle;
        ThemeComboBox.SelectedIndex = (int)_configuration.Theme;
        if (!TryApplyAccent(_configuration.AccentColor))
        {
            _configuration.AccentColor = "#FF6ED6A9";
            TryApplyAccent(_configuration.AccentColor);
        }
        ApplyTheme();
        ApplyLogoStyle();
        _windowService.ResizeAndPosition(CollapsedWidth, CollapsedHeight);
        _settingsLoaded = true;
        await SaveConfigurationAsync();

        try
        {
            await _mediaService.InitializeAsync();
        }
        catch
        {
            ApplyMediaSnapshot(MediaSnapshot.Empty);
        }
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        if (_islandManager.State == IslandState.Collapsed)
        {
            _islandManager.TransitionTo(IslandState.Preview);
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging && !_isFileDragInProgress && _islandManager.State != IslandState.Collapsed &&
            !_interactiveAppPanel.IsAttached)
        {
            _collapseTimer.Start();
        }
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        if (_islandManager.State == IslandState.Collapsed)
        {
            _islandManager.TransitionTo(IslandState.Preview);
        }

        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        var added = await AddFilesAsync(paths);
        _islandManager.TransitionTo(IslandState.Expanded);
        ShowPanel(FilesPanel);
        FileDockStatus.Text = added > 0
            ? $"Saved {added} file{(added == 1 ? string.Empty : "s")} to the shelf"
            : "Those files are already in the shelf";
        e.Handled = true;
    }

    private async void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractiveSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _collapseTimer.Stop();
        _isDragging = true;
        try
        {
            DragMove();
            var offsets = _windowService.CaptureDraggedPosition(ActualWidth, ActualHeight);
            _configuration.HorizontalOffset = offsets.HorizontalOffset;
            _configuration.VerticalOffset = offsets.VerticalOffset;
            HorizontalOffsetSlider.Value = offsets.HorizontalOffset;
            VerticalOffsetSlider.Value = offsets.VerticalOffset;
            await SaveConfigurationAsync();
        }
        catch (InvalidOperationException)
        {
            // DragMove throws when the mouse was released before it acquired capture.
        }
        finally
        {
            _isDragging = false;
        }
    }

    private static bool IsInteractiveSource(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Button or Slider or ComboBox or TextBox or ListViewItem)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void CollapseTimer_Tick(object? sender, EventArgs e)
    {
        if (_isDragging || _isFileDragInProgress || IsMouseOver || _interactiveAppPanel.IsAttached ||
            EdgeComboBox.IsDropDownOpen || AlignmentComboBox.IsDropDownOpen)
        {
            _collapseTimer.Stop();
            return;
        }

        _collapseTimer.Stop();
        _islandManager.TransitionTo(IslandState.Collapsed);
    }

    private void OnIslandStateChanged(object? sender, IslandState state)
    {
        var (width, height) = state switch
        {
            IslandState.Preview => (PreviewWidth, PreviewHeight),
            IslandState.Expanded or IslandState.Interaction or IslandState.Notification =>
                (ExpandedWidth, ExpandedHeight),
            _ => (CollapsedWidth, CollapsedHeight)
        };

        _isAnimating = true;
        _interactiveAppPanel.Suspend();
        ShowStateContent(state);
        AnimateIslandResize(width, height, state);
    }

    private void AnimateIslandResize(double targetWidth, double targetHeight, IslandState state)
    {
        var version = ++_resizeAnimationVersion;
        var currentWidth = Math.Max(1, ActualWidth);
        var currentHeight = Math.Max(1, ActualHeight);
        var scale = (ScaleTransform)IslandSurface.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1;
        scale.ScaleY = 1;

        IslandSurface.CornerRadius = state switch
        {
            IslandState.Collapsed => new CornerRadius(13),
            IslandState.Preview => new CornerRadius(22),
            _ => new CornerRadius(32)
        };

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(235));
        var expanding = (targetWidth * targetHeight) >= (currentWidth * currentHeight);

        if (expanding)
        {
            _windowService.ResizeAndPosition(targetWidth, targetHeight);
            scale.ScaleX = Math.Clamp(currentWidth / targetWidth, 0.05, 1);
            scale.ScaleY = Math.Clamp(currentHeight / targetHeight, 0.05, 1);
            var xAnimation = new DoubleAnimation(1, duration) { EasingFunction = easing };
            var yAnimation = new DoubleAnimation(1, duration) { EasingFunction = easing };
            yAnimation.Completed += (_, _) => FinishResizeAnimation(version, targetWidth, targetHeight);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, xAnimation, HandoffBehavior.SnapshotAndReplace);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, yAnimation, HandoffBehavior.SnapshotAndReplace);
            return;
        }

        var targetScaleX = Math.Clamp(targetWidth / currentWidth, 0.05, 1);
        var targetScaleY = Math.Clamp(targetHeight / currentHeight, 0.05, 1);
        var collapseX = new DoubleAnimation(targetScaleX, duration) { EasingFunction = easing };
        var collapseY = new DoubleAnimation(targetScaleY, duration) { EasingFunction = easing };
        collapseY.Completed += (_, _) => FinishResizeAnimation(version, targetWidth, targetHeight);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, collapseX, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, collapseY, HandoffBehavior.SnapshotAndReplace);
    }

    private void FinishResizeAnimation(int version, double targetWidth, double targetHeight)
    {
        if (version != _resizeAnimationVersion)
        {
            return;
        }

        var scale = (ScaleTransform)IslandSurface.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1;
        scale.ScaleY = 1;
        _windowService.ResizeAndPosition(targetWidth, targetHeight);
        _windowService.RemoveNativeBorder();
        _isAnimating = false;
        UpdateInteractivePanel();
    }

    private void ShowStateContent(IslandState state)
    {
        var show = state switch
        {
            IslandState.Preview => PreviewContent,
            IslandState.Expanded or IslandState.Interaction or IslandState.Notification => ExpandedContent,
            _ => CollapsedContent
        };

        foreach (var element in new FrameworkElement[] { CollapsedContent, PreviewContent, ExpandedContent })
        {
            if (element == show)
            {
                element.Visibility = Visibility.Visible;
                element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            }
            else
            {
                element.BeginAnimation(OpacityProperty, null);
                element.Opacity = 0;
                element.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void FullExpandButton_Click(object sender, RoutedEventArgs e)
    {
        _collapseTimer.Stop();
        _islandManager.TransitionTo(IslandState.Expanded);
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e) =>
        _islandManager.TransitionTo(IslandState.Collapsed);

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveConfigurationAsync();
        Close();
    }

    private void MediaNavButton_Click(object sender, RoutedEventArgs e) => ShowPanel(MediaPanel);
    private void FilesNavButton_Click(object sender, RoutedEventArgs e) => ShowPanel(FilesPanel);
    private void SettingsNavButton_Click(object sender, RoutedEventArgs e) => ShowPanel(SettingsPanel);
    private async void AppsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(AppsPanel);
        RefreshRunningApplications();
        if (!_installedAppsLoaded)
        {
            await RefreshInstalledApplicationsAsync();
        }
    }

    private void ClipboardNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(ClipboardPanel);
        PasteClipboardText(false);
    }

    private void ShowPanel(UIElement panel)
    {
        if (panel != AppsPanel)
        {
            _interactiveAppPanel.Suspend();
        }
        MediaPanel.Visibility = panel == MediaPanel ? Visibility.Visible : Visibility.Collapsed;
        FilesPanel.Visibility = panel == FilesPanel ? Visibility.Visible : Visibility.Collapsed;
        ClipboardPanel.Visibility = panel == ClipboardPanel ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = panel == SettingsPanel ? Visibility.Visible : Visibility.Collapsed;
        AppsPanel.Visibility = panel == AppsPanel ? Visibility.Visible : Visibility.Collapsed;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateInteractivePanel));
    }

    private void OnMediaChanged(object? sender, MediaSnapshot snapshot) =>
        Dispatcher.InvokeAsync(() => ApplyMediaSnapshot(snapshot));

    private void ApplyMediaSnapshot(MediaSnapshot snapshot)
    {
        _currentMediaSnapshot = snapshot;
        TrackTitle.Text = snapshot.Title;
        TrackArtist.Text = snapshot.Artist;
        AlbumTitle.Text = snapshot.Album;
        AlbumTitle.Visibility = string.IsNullOrWhiteSpace(snapshot.Album) ? Visibility.Collapsed : Visibility.Visible;
        PreviewTitle.Text = snapshot.HasSession ? snapshot.Title : "Dynamic Island";
        PreviewArtist.Text = snapshot.HasSession ? snapshot.Artist : "Ready · drag to reposition";
        PlayPauseIcon.Text = snapshot.IsPlaying ? "Ⅱ" : "▶";
        PreviewPlayPauseText.Text = snapshot.IsPlaying ? "Ⅱ" : "▶";
        PreviousButton.IsEnabled = snapshot.CanPrevious;
        PlayPauseButton.IsEnabled = snapshot.CanPlayPause;
        NextButton.IsEnabled = snapshot.CanNext;
        PreviewPreviousButton.IsEnabled = snapshot.CanPrevious;
        PreviewPlayPauseButton.IsEnabled = snapshot.CanPlayPause;
        PreviewNextButton.IsEnabled = snapshot.CanNext;
        ApplyPlaybackIndicator();

        _currentArtwork = CreateImage(snapshot.Thumbnail);
        Artwork.Source = _currentArtwork;
        ArtworkFallbackIcon.Visibility = _currentArtwork is null ? Visibility.Visible : Visibility.Collapsed;
        ApplyLogoStyle();
    }

    private static BitmapImage? CreateImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private async void PreviousButton_Click(object sender, RoutedEventArgs e) => await _mediaService.PreviousAsync();
    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e) => await _mediaService.TogglePlayPauseAsync();
    private async void NextButton_Click(object sender, RoutedEventArgs e) => await _mediaService.NextAsync();

    private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Multiselect = true, CheckFileExists = true };
        if (picker.ShowDialog(this) == true)
        {
            await AddFilesAsync(picker.FileNames);
        }
    }

    private void FilesDropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FilesDropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await AddFilesAsync(paths);
        }
        e.Handled = true;
    }

    private async void PasteFilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsFileDropList())
        {
            await AddFilesAsync(Clipboard.GetFileDropList().Cast<string>());
        }
    }

    private async Task<int> AddFilesAsync(IEnumerable<string> paths)
    {
        var changed = false;
        var added = 0;
        foreach (var path in paths.Where(File.Exists))
        {
            var sourcePath = Path.GetFullPath(path);
            if (_files.Any(item => string.Equals(
                    string.IsNullOrWhiteSpace(item.OriginalPath) ? item.FullPath : item.OriginalPath,
                    sourcePath,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                var stored = await _fileDockStorage.StoreAsync(sourcePath);
                if (stored is null)
                {
                    continue;
                }

                _files.Add(stored);
            }
            catch (Exception exception)
            {
                FileDockStatus.Text = $"Could not store {Path.GetFileName(path)}: {exception.Message}";
                continue;
            }

            changed = true;
            added++;
        }

        if (changed)
        {
            await SaveConfigurationAsync();
            FileDockStatus.Text = $"Stored {added} file{(added == 1 ? string.Empty : "s")} in the island's private shelf";
        }

        return added;
    }

    private void FileList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _fileDragItem = null;
        if (e.OriginalSource is not DependencyObject source ||
            FindAncestor<Button>(source) is not null)
        {
            return;
        }

        var row = ItemsControl.ContainerFromElement(FileList, source) as ListViewItem;
        _fileDragItem = row?.DataContext as DockedFileItem;
        _fileDragStart = e.GetPosition(FileList);
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T found) return found;
        }

        return null;
    }

    private void FileList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isFileDragInProgress || _fileDragItem is null)
        {
            return;
        }

        var current = e.GetPosition(FileList);
        if (Math.Abs(current.X - _fileDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _fileDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _fileDragItem;
        _fileDragItem = null;
        var storedPath = _fileDockStorage.GetDragOutPath(item);
        if (storedPath is null)
        {
            FileDockStatus.Text = "Stored file is missing. Remove it and add the file again.";
            return;
        }

        var files = new StringCollection { storedPath };
        var data = new DataObject();
        data.SetFileDropList(files);
        _collapseTimer.Stop();
        _isFileDragInProgress = true;
        try
        {
            var result = DragDrop.DoDragDrop(FileList, data, DragDropEffects.Copy);
            FileDockStatus.Text = result == DragDropEffects.None
                ? "Drop cancelled. Use Copy or Open stored folder if this site does not accept a drop."
                : $"Sent {item.Name} from the stored folder";
        }
        catch (Exception exception)
        {
            FileDockStatus.Text = $"Could not drag {item.Name}: {exception.Message}";
        }
        finally
        {
            _isFileDragInProgress = false;
            if (!IsMouseOver && _islandManager.State != IslandState.Collapsed)
            {
                _collapseTimer.Start();
            }
        }
    }

    private async void AddAppButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Filter = "Windows applications (*.exe)|*.exe",
            Multiselect = false,
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        if (_applications.Any(item => string.Equals(
                item.ExecutablePath,
                picker.FileName,
                StringComparison.OrdinalIgnoreCase)))
        {
            AppRunnerStatus.Text = "That application is already in the runner";
            return;
        }

        var item = new AppLauncherItem
        {
            Name = Path.GetFileNameWithoutExtension(picker.FileName),
            ExecutablePath = picker.FileName
        };
        _applications.Add(item);
        AppList.SelectedItem = item;
        AppRunnerStatus.Text = $"Added {item.Name}";
        await SaveConfigurationAsync();
    }

    private async void RemoveAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is not AppLauncherItem item)
        {
            AppRunnerStatus.Text = "Select an application first";
            return;
        }

        _applications.Remove(item);
        AppRunnerStatus.Text = $"Removed {item.Name} from the runner";
        await SaveConfigurationAsync();
    }

    private void LaunchAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is not AppLauncherItem item)
        {
            AppRunnerStatus.Text = "Select an application first";
            return;
        }

        Process.Start(new ProcessStartInfo(item.ExecutablePath, item.Arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(item.ExecutablePath)
        });
        AppRunnerStatus.Text = $"Opened {item.Name} normally";
    }

    private void RefreshRunningAppsButton_Click(object sender, RoutedEventArgs e) => RefreshRunningApplications();

    private void RefreshRunningApplications()
    {
        var selectedHandle = (RunningAppsComboBox.SelectedItem as RunningApplicationInfo)?.WindowHandle;
        var windows = _appCatalog.DiscoverRunningWindows(Environment.ProcessId);

        _runningApplications.Clear();
        foreach (var window in windows)
        {
            _runningApplications.Add(window);
        }

        RunningAppsComboBox.SelectedItem = _runningApplications.FirstOrDefault(item => item.WindowHandle == selectedHandle)
                                           ?? _runningApplications.FirstOrDefault();
        AppRunnerStatus.Text = _runningApplications.Count == 0
            ? "No compatible running windows were found"
            : $"Found {_runningApplications.Count} running window{(_runningApplications.Count == 1 ? string.Empty : "s")}";
    }

    private void UseRunningAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (RunningAppsComboBox.SelectedItem is not RunningApplicationInfo application)
        {
            AppRunnerStatus.Text = "Select a running window first";
            return;
        }

        UseWindowInPanel(application.WindowHandle, application.DisplayName);
    }

    private async void RefreshInstalledAppsButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshInstalledApplicationsAsync();

    private async Task RefreshInstalledApplicationsAsync()
    {
        AppRunnerStatus.Text = "Loading installed applications…";
        var applications = await _appCatalog.DiscoverAsync();
        _installedApplications.Clear();
        foreach (var application in applications)
        {
            _installedApplications.Add(application);
        }

        InstalledAppsComboBox.SelectedItem = _installedApplications.FirstOrDefault();
        _installedAppsLoaded = true;
        AppRunnerStatus.Text = $"Loaded {_installedApplications.Count} installed applications";
    }

    private async void RunInstalledAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (InstalledAppsComboBox.SelectedItem is not InstalledApplicationInfo application)
        {
            AppRunnerStatus.Text = "Select an installed application first";
            return;
        }

        AppRunnerStatus.Text = $"Starting {application.Name}…";
        var window = await _appCatalog.LaunchAndFindWindowAsync(application);
        if (window == 0)
        {
            AppRunnerStatus.Text = $"{application.Name} did not expose a usable desktop window";
            return;
        }

        UseWindowInPanel(window, application.Name);
    }

    private async void UseAppInPanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is not AppLauncherItem item)
        {
            AppRunnerStatus.Text = "Select a saved application first";
            return;
        }

        AppRunnerStatus.Text = $"Starting {item.Name}…";
        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo(item.ExecutablePath, item.Arguments)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(item.ExecutablePath)
            });
        }
        catch (Exception exception)
        {
            AppRunnerStatus.Text = $"Could not start {item.Name}: {exception.Message}";
            return;
        }

        var window = process is null ? 0 : await FindMainWindowAsync(process);
        if (window == 0)
        {
            AppRunnerStatus.Text = $"{item.Name} did not expose a usable desktop window";
            return;
        }

        UseWindowInPanel(window, item.Name);
    }

    private void UseWindowInPanel(nint window, string name)
    {
        _collapseTimer.Stop();
        _islandManager.TransitionTo(IslandState.Expanded);
        ShowPanel(AppsPanel);
        _ = _virtualDesktopService.MoveToIslandDesktop(window, this);
        AppHostPlaceholder.Visibility = Visibility.Collapsed;
        UpdateLayout();
        if (_interactiveAppPanel.Attach(window, AppHostViewport))
        {
            if (_isAnimating) _interactiveAppPanel.Suspend();
            AppRunnerStatus.Text = $"Using {name} in the panel";
            return;
        }

        ShowAppHostPlaceholder(_interactiveAppPanel.LastError ?? $"Could not place {name} in interactive panel mode");
    }

    private static async Task<nint> FindMainWindowAsync(Process process)
    {
        try
        {
            await Task.Run(() => process.WaitForInputIdle(8000));
        }
        catch
        {
            // Some application types do not expose an input-idle state.
        }

        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (process.HasExited)
            {
                return 0;
            }

            process.Refresh();
            if (process.MainWindowHandle != 0)
            {
                return process.MainWindowHandle;
            }

            await Task.Delay(125);
        }

        return 0;
    }

    private void DetachAppButton_Click(object sender, RoutedEventArgs e)
    {
        _interactiveAppPanel.Detach();
        ShowAppHostPlaceholder("The hosted application was detached");
    }

    private void ShowAppHostPlaceholder(string status)
    {
        _interactiveAppPanel.Detach();
        AppHostPlaceholder.Visibility = Visibility.Visible;
        AppRunnerStatus.Text = status;
    }

    private void UpdateInteractivePanel()
    {
        if (_isAnimating || WindowState == WindowState.Minimized) return;
        if (_interactiveAppPanel.IsAttached)
        {
            if (_islandManager.State == IslandState.Expanded && AppsPanel.Visibility == Visibility.Visible)
            {
                _interactiveAppPanel.Resume(AppHostViewport);
            }
            else
            {
                _interactiveAppPanel.Suspend();
            }
        }
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string path && File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    private void CopyFileButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string path || !File.Exists(path))
        {
            return;
        }

        var files = new StringCollection { path };
        Clipboard.SetFileDropList(files);
    }

    private void OpenFileDockFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_fileDockStorage.StorageRoot);
        Process.Start(new ProcessStartInfo(_fileDockStorage.StorageRoot) { UseShellExecute = true });
    }

    private async void RemoveFileButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string path)
        {
            return;
        }

        var item = _files.FirstOrDefault(file => string.Equals(file.FullPath, path, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            try
            {
                _fileDockStorage.RemoveManagedCopy(item);
            }
            catch (Exception exception)
            {
                FileDockStatus.Text = $"Could not remove the stored copy: {exception.Message}";
                return;
            }

            _files.Remove(item);
            FileDockStatus.Text = $"Removed {item.Name} from the shelf";
            await SaveConfigurationAsync();
        }
    }

    private void PasteTextButton_Click(object sender, RoutedEventArgs e) => PasteClipboardText(true);

    private void PasteClipboardText(bool showEmptyMessage)
    {
        if (Clipboard.ContainsText())
        {
            ClipboardTextBox.Text = Clipboard.GetText();
            ClipboardStatus.Text = "Pasted from Windows clipboard";
        }
        else if (showEmptyMessage)
        {
            ClipboardStatus.Text = "No text is currently on the clipboard";
        }
    }

    private void CopyTextButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ClipboardTextBox.Text);
        ClipboardStatus.Text = "Copied to Windows clipboard";
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var opacity = Math.Clamp(e.NewValue / 100, 0.1, 1);
        ApplySurfaceOpacity(opacity);
        if (!_settingsLoaded) return;
        _configuration.SurfaceOpacity = opacity;
        QueueSettingsSave();
    }

    private void ApplySurfaceOpacity(double opacity)
    {
        var baseColor = _configuration.Theme == IslandTheme.Light
            ? Color.FromRgb(246, 246, 248)
            : Color.FromRgb(17, 17, 19);
        IslandSurface.Background = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(255 * opacity), baseColor.R, baseColor.G, baseColor.B));
        OpacityValueText.Text = $"{opacity:P0}";
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsLoaded || ThemeComboBox.SelectedIndex < 0)
        {
            return;
        }

        _configuration.Theme = (IslandTheme)ThemeComboBox.SelectedIndex;
        ApplyTheme();
        QueueSettingsSave();
    }

    private void ApplyTheme()
    {
        if (_configuration.Theme == IslandTheme.Light)
        {
            SetThemeBrush("PrimaryTextBrush", Color.FromRgb(24, 24, 27));
            SetThemeBrush("SecondaryTextBrush", Color.FromRgb(95, 95, 103));
            SetThemeBrush("ControlSurfaceBrush", Color.FromArgb(204, 226, 226, 231));
            SetThemeBrush("PanelSurfaceBrush", Color.FromArgb(220, 240, 240, 243));
            SetThemeBrush("InputSurfaceBrush", Color.FromArgb(235, 255, 255, 255));
            SetThemeBrush("SubtleSurfaceBrush", Color.FromArgb(230, 225, 225, 230));
            SetThemeBrush("DropSurfaceBrush", Color.FromArgb(180, 231, 231, 235));
            SetThemeBrush("DividerBrush", Color.FromArgb(190, 142, 142, 147));
            SetThemeBrush("HostSurfaceBrush", Color.FromRgb(247, 247, 249));
            SetThemeBrush("PrimaryActionBrush", Color.FromRgb(24, 24, 27));
            SetThemeBrush("PrimaryActionTextBrush", Colors.White);
        }
        else
        {
            SetThemeBrush("PrimaryTextBrush", Color.FromRgb(247, 247, 248));
            SetThemeBrush("SecondaryTextBrush", Color.FromRgb(165, 165, 170));
            SetThemeBrush("ControlSurfaceBrush", Color.FromArgb(230, 41, 41, 46));
            SetThemeBrush("PanelSurfaceBrush", Color.FromArgb(128, 41, 41, 46));
            SetThemeBrush("InputSurfaceBrush", Color.FromArgb(179, 41, 41, 46));
            SetThemeBrush("SubtleSurfaceBrush", Color.FromArgb(214, 41, 41, 46));
            SetThemeBrush("DropSurfaceBrush", Color.FromArgb(64, 47, 47, 53));
            SetThemeBrush("DividerBrush", Color.FromArgb(102, 91, 91, 99));
            SetThemeBrush("HostSurfaceBrush", Color.FromRgb(9, 9, 11));
            SetThemeBrush("PrimaryActionBrush", Color.FromRgb(244, 244, 245));
            SetThemeBrush("PrimaryActionTextBrush", Color.FromRgb(17, 17, 19));
        }

        ApplySurfaceOpacity(_configuration.SurfaceOpacity);

    }

    private static void SetThemeBrush(string resourceKey, Color color)
    {
        if (Application.Current.Resources[resourceKey] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
        }
        else
        {
            Application.Current.Resources[resourceKey] = new SolidColorBrush(color);
        }
    }

    private void ApplyAccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyAccent(AccentColorTextBox.Text))
        {
            AccentStatusText.Text = "Enter a valid color such as #6ED6A9";
            return;
        }

        _configuration.AccentColor = AccentColorTextBox.Text;
        AccentStatusText.Text = "Accent updated";
        ApplyPlaybackIndicator();
        QueueSettingsSave();
    }

    private void AccentPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string color)
        {
            return;
        }

        AccentColorTextBox.Text = color;
        ApplyAccentButton_Click(sender, e);
    }

    private void PickAccentButton_Click(object sender, RoutedEventArgs e)
    {
        var current = Application.Current.Resources["AccentBrush"] is SolidColorBrush brush
            ? brush.Color
            : Color.FromRgb(110, 214, 169);
        if (!ColorPickerService.TryPick(this, current, out var selected))
        {
            return;
        }

        AccentColorTextBox.Text = $"#{selected.R:X2}{selected.G:X2}{selected.B:X2}";
        ApplyAccentButton_Click(sender, e);
    }

    private bool TryApplyAccent(string? value)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value ?? string.Empty);
            var normalized = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            Application.Current.Resources["AccentBrush"] = new SolidColorBrush(color);
            AccentColorTextBox.Text = normalized;
            _configuration.AccentColor = normalized;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyPlaybackIndicator()
    {
        PlaybackIndicator.Fill = _currentMediaSnapshot.HasSession
            ? _currentMediaSnapshot.IsPlaying
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : new SolidColorBrush(Color.FromRgb(251, 191, 36))
            : new SolidColorBrush(Color.FromRgb(113, 113, 120));
    }

    private void LogoStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsLoaded || LogoStyleComboBox.SelectedIndex < 0)
        {
            return;
        }

        _configuration.LogoStyle = (IslandLogoStyle)LogoStyleComboBox.SelectedIndex;
        ApplyLogoStyle();
        QueueSettingsSave();
    }

    private void ApplyLogoStyle()
    {
        var useArtwork = _configuration.LogoStyle == IslandLogoStyle.AlbumArtwork && _currentArtwork is not null;
        var useBrand = _configuration.LogoStyle == IslandLogoStyle.BrandMark;
        var showImage = useArtwork || useBrand;
        MiniLogoArtwork.Source = useBrand ? _brandLogo : useArtwork ? _currentArtwork : null;
        MiniLogoArtwork.Visibility = showImage ? Visibility.Visible : Visibility.Collapsed;
        MiniLogoText.Visibility = showImage ? Visibility.Collapsed : Visibility.Visible;
        PreviewArtwork.Source = useBrand ? _brandLogo : useArtwork ? _currentArtwork : null;
        PreviewArtwork.Visibility = showImage ? Visibility.Visible : Visibility.Collapsed;
        PreviewFallbackIcon.Visibility = showImage ? Visibility.Collapsed : Visibility.Visible;
        MiniLogoContainer.Background = useBrand
            ? new SolidColorBrush(Color.FromRgb(26, 28, 31))
            : (Brush)Application.Current.Resources["PrimaryActionBrush"];
        MiniLogoContainer.Width = useBrand ? 22 : 16;
        PreviewLogoContainer.Background = useBrand
            ? new SolidColorBrush(Color.FromRgb(26, 28, 31))
            : (Brush)Application.Current.Resources["SubtleSurfaceBrush"];

        var (text, miniSize, previewSize) = _configuration.LogoStyle switch
        {
            IslandLogoStyle.MusicNote => ("♪", 10d, 22d),
            IslandLogoStyle.MinimalDot => ("●", 8d, 18d),
            IslandLogoStyle.AlbumArtwork => ("♪", 10d, 22d),
            IslandLogoStyle.BrandMark => ("DI", 7d, 12d),
            _ => ("DI", 7d, 12d)
        };
        MiniLogoText.Text = text;
        MiniLogoText.FontSize = miniSize;
        PreviewFallbackIcon.Text = text;
        PreviewFallbackIcon.FontSize = previewSize;
    }

    private void EdgeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsLoaded || EdgeComboBox.SelectedIndex < 0) return;
        _configuration.Edge = (IslandEdge)EdgeComboBox.SelectedIndex;
        ApplyWindowPlacement();
        QueueSettingsSave();
    }

    private void AlignmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsLoaded || AlignmentComboBox.SelectedIndex < 0) return;
        _configuration.Alignment = (IslandAlignment)AlignmentComboBox.SelectedIndex;
        ApplyWindowPlacement();
        QueueSettingsSave();
    }

    private void HorizontalOffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = (int)Math.Round(e.NewValue);
        HorizontalOffsetText.Text = $"Horizontal: {value:+0;-0;0} px";
        if (!_settingsLoaded) return;
        _configuration.HorizontalOffset = value;
        ApplyWindowPlacement();
        QueueSettingsSave();
    }

    private void VerticalOffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = (int)Math.Round(e.NewValue);
        VerticalOffsetText.Text = $"Vertical: {value:+0;-0;0} px";
        if (!_settingsLoaded) return;
        _configuration.VerticalOffset = value;
        ApplyWindowPlacement();
        QueueSettingsSave();
    }

    private void ApplyWindowPlacement()
    {
        _windowService.Edge = _configuration.Edge;
        _windowService.Alignment = _configuration.Alignment;
        _windowService.HorizontalOffset = _configuration.HorizontalOffset;
        _windowService.VerticalOffset = _configuration.VerticalOffset;
        _windowService.ResizeAndPosition(ActualWidth, ActualHeight);
    }

    private void ResetPositionButton_Click(object sender, RoutedEventArgs e)
    {
        _configuration.Edge = IslandEdge.Top;
        _configuration.Alignment = IslandAlignment.Center;
        _configuration.HorizontalOffset = 0;
        _configuration.VerticalOffset = 8;
        EdgeComboBox.SelectedIndex = 0;
        AlignmentComboBox.SelectedIndex = 1;
        HorizontalOffsetSlider.Value = 0;
        VerticalOffsetSlider.Value = 8;
        ApplyWindowPlacement();
        QueueSettingsSave();
    }

    private void QueueSettingsSave()
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private async void SettingsSaveTimer_Tick(object? sender, EventArgs e)
    {
        _settingsSaveTimer.Stop();
        await SaveConfigurationAsync();
    }

    private async Task SaveConfigurationAsync()
    {
        _configuration.DockedFiles = _files.ToList();
        _configuration.Applications = _applications.ToList();
        await _configurationService.SaveAsync(_configuration);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _topmostTimer.Stop();
        _collapseTimer.Stop();
        _settingsSaveTimer.Stop();
        _interactiveAppPanel.Detach();

        _mediaService.Dispose();
    }
}
