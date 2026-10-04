using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DynamicIsland.Core;
using DynamicIsland.Models;
using DynamicIsland.Services;
using DynamicIsland.UI;
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
    private const string PinningUnavailableMessage = "Windows blocked all-desktop pinning; the island may remain on one desktop.";

    private readonly IslandManager _islandManager = new();
    private readonly MediaSessionService _mediaService = new();
    private readonly BitmapImage _brandLogo = new(new Uri("pack://application:,,,/Assets/island-logo.png", UriKind.Absolute));
    private readonly ConfigurationService _configurationService = new();
    private readonly FileDockStorageService _fileDockStorage = new();
    private readonly MiniLogoStorageService _miniLogoStorage = new();
    private readonly AppCatalogService _appCatalog = new();
    private readonly InteractiveAppPanelService _interactiveAppPanel = new();
    private readonly LiveAppPreviewService _liveAppPreview = new();
    private readonly VirtualDesktopService _virtualDesktopService = new();
    private readonly ObservableCollection<DockedFileItem> _files = [];
    private readonly ObservableCollection<AppLauncherItem> _applications = [];
    private readonly ObservableCollection<RunningApplicationInfo> _runningApplications = [];
    private readonly ObservableCollection<InstalledApplicationInfo> _installedApplications = [];
    private readonly WindowService _windowService;
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private readonly DispatcherTimer _settingsSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _mediaProgressTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _powerTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly ClipboardService _clipboardService = new();

    private AppConfiguration _configuration = new();
    private bool _settingsLoaded;
    private bool _isDragging;
    private bool _isFileDragInProgress;
    private bool _isLogoEditorOpen;
    private bool _suppressHoverUntilPointerLeaves;
    private bool _isSeeking;
    private bool _isUpdatingSeek;
    private DockedFileItem? _fileDragItem;
    private BitmapSource? _currentArtwork;
    private BitmapSource? _customMiniLogo;
    private BitmapSource? _clipboardImage;
    private List<string> _clipboardFiles = [];
    private ClipboardKind _clipboardKind = ClipboardKind.Text;
    private bool _chargingBorderActive;
    private bool _isCharging;
    private MediaSnapshot _currentMediaSnapshot = MediaSnapshot.Empty;
    private Point _fileDragStart;
    private int _resizeAnimationVersion;
    private int _panelAnimationVersion;
    private UIElement? _activePanel;
    private FrameworkElement? _visibleStateContent;
    private bool _installedAppsLoaded;
    private bool _isAnimating;
    private bool _isPinnedToAllDesktops;
    private int _desktopPinAttempts;
    private int _desktopPinTimerTicks;
    private readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    private enum ClipboardKind { Text, Image, Files }

    public MainWindow()
    {
        InitializeComponent();
        _activePanel = MediaPanel;
        FileList.ItemsSource = _files;
        AppList.ItemsSource = _applications;
        RunningAppsComboBox.ItemsSource = _runningApplications;
        InstalledAppsComboBox.ItemsSource = _installedApplications;
        OpacitySlider.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler(OpacitySlider_DragCompleted));
        SeekSlider.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler((thumb, args) =>
            {
                if (_isSeeking) _ = CommitSeekAsync();
            }));
        IslandSurface.RenderTransformOrigin = new Point(0.5, 0);
        IslandSurface.RenderTransform = new ScaleTransform(1, 1);
        LiquidBorder.RenderTransformOrigin = IslandSurface.RenderTransformOrigin;
        LiquidBorder.RenderTransform = IslandSurface.RenderTransform;
        UpdateLiquidBorder(IslandState.Collapsed);
        LocationChanged += (_, _) => UpdateInteractivePanel();
        SizeChanged += (_, _) => UpdateInteractivePanel();
        AppHostViewport.SizeChanged += (_, _) => UpdateInteractivePanel();

        _windowService = new WindowService(this);
        _windowService.Configure();
        _islandManager.StateChanged += OnIslandStateChanged;
        _mediaService.MediaChanged += OnMediaChanged;
        _collapseTimer.Tick += CollapseTimer_Tick;
        _settingsSaveTimer.Tick += SettingsSaveTimer_Tick;
        _mediaProgressTimer.Tick += (_, _) => ApplyMediaProgress();
        _mediaProgressTimer.Start();
        _powerTimer.Tick += (_, _) => RefreshChargingBorder();
        _topmostTimer.Tick += (_, _) =>
        {
            if (!IsVisible || WindowState == WindowState.Minimized) return;
            if (++_desktopPinTimerTicks >= (_isPinnedToAllDesktops ? 60 : 10))
            {
                _desktopPinTimerTicks = 0;
                _isPinnedToAllDesktops = _virtualDesktopService.TryPinIslandToAllDesktops(this);
                if (!_isPinnedToAllDesktops && ++_desktopPinAttempts == 20)
                    IslandSurface.ToolTip = PinningUnavailableMessage;
                else if (_isPinnedToAllDesktops)
                {
                    _desktopPinAttempts = 0;
                    if (Equals(IslandSurface.ToolTip, PinningUnavailableMessage))
                        IslandSurface.ToolTip = null;
                }
            }
            _windowService.EnsureTopmost();
            if (!_isAnimating && _islandManager.State == IslandState.Expanded &&
                _activePanel == AppsPanel && AppsPanel.Visibility == Visibility.Visible)
            {
                _interactiveAppPanel.UpdateBounds(AppHostViewport);
                _liveAppPreview.UpdateBounds(AppHostViewport);
            }
        };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                _interactiveAppPanel.Suspend();
                _liveAppPreview.Suspend();
            }
            else UpdateInteractivePanel();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _windowService.RemoveNativeBorder();
        _isPinnedToAllDesktops = _virtualDesktopService.TryPinIslandToAllDesktops(this);
        _topmostTimer.Start();
        RefreshChargingBorder();
        _powerTimer.Start();
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
        if (_configuration.ConfigurationVersion < 8)
            _configuration.ConfigurationVersion = 8;

        _configuration.SurfaceOpacity = Math.Clamp(_configuration.SurfaceOpacity, 0.1, 1);
        if (!Enum.IsDefined(_configuration.SurfaceStyle))
            _configuration.SurfaceStyle = IslandSurfaceStyle.Solid;
        if (!Enum.IsDefined(_configuration.GradientDirection))
            _configuration.GradientDirection = IslandGradientDirection.Diagonal;
        if (!TryParseSurfaceColor(_configuration.GradientStartColor, out _))
            _configuration.GradientStartColor = "#FF334654";
        if (!TryParseSurfaceColor(_configuration.GradientEndColor, out _))
            _configuration.GradientEndColor = "#FF161821";
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
        SurfaceStyleComboBox.SelectedIndex = (int)_configuration.SurfaceStyle;
        GradientDirectionComboBox.SelectedIndex = (int)_configuration.GradientDirection;
        GradientStartColorTextBox.Text = _configuration.GradientStartColor;
        GradientEndColorTextBox.Text = _configuration.GradientEndColor;
        GradientOptions.Visibility = _configuration.SurfaceStyle == IslandSurfaceStyle.Gradient
            ? Visibility.Visible : Visibility.Collapsed;
        try
        {
            _customMiniLogo = _miniLogoStorage.Load(_configuration.MiniLogoImagePath);
            MiniLogoStatusText.Text = _customMiniLogo is null
                ? "Using the selected island logo." : "Using your saved mini picture.";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _customMiniLogo = null;
            MiniLogoStatusText.Text = "Saved picture could not be loaded; using the selected island logo.";
        }
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
        if (_suppressHoverUntilPointerLeaves) return;
        if (_islandManager.State == IslandState.Collapsed)
        {
            _islandManager.TransitionTo(IslandState.Preview);
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_suppressHoverUntilPointerLeaves && !_isAnimating &&
            _islandManager.State == IslandState.Collapsed)
            _suppressHoverUntilPointerLeaves = false;

        if (!_isDragging && !_isFileDragInProgress && !_isLogoEditorOpen && _islandManager.State != IslandState.Collapsed &&
            !_interactiveAppPanel.IsAttached && !_liveAppPreview.IsAttached)
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
        if (_isDragging || _isFileDragInProgress || _isLogoEditorOpen || IsMouseOver || _interactiveAppPanel.IsAttached || _liveAppPreview.IsAttached ||
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
        UpdateLiquidBorder(state);
        var (width, height) = state switch
        {
            IslandState.Preview => (PreviewWidth, PreviewHeight),
            IslandState.Expanded or IslandState.Interaction or IslandState.Notification =>
                (ExpandedWidth, ExpandedHeight),
            _ => (CollapsedWidth, CollapsedHeight)
        };

        _isAnimating = true;
        _interactiveAppPanel.Suspend();
        _liveAppPreview.Suspend();
        ShowStateContent(state);
        AnimateIslandResize(width, height, state);
    }

    private void UpdateLiquidBorder(IslandState state)
    {
        var showBorder = state is IslandState.Collapsed or IslandState.Preview;
        var charging = state == IslandState.Collapsed && _isCharging;
        LiquidBorder.Visibility = showBorder ? Visibility.Visible : Visibility.Collapsed;
        LiquidBorder.CornerRadius = state switch
        {
            IslandState.Collapsed => new CornerRadius(13),
            IslandState.Preview => new CornerRadius(22),
            _ => new CornerRadius(32)
        };
        if (charging != _chargingBorderActive)
        {
            _chargingBorderActive = charging;
            LiquidBorder.BorderBrush = CreateLiquidBorderBrush(charging);
        }
        if (LiquidBorder.BorderBrush is not LinearGradientBrush brush ||
            brush.RelativeTransform is not RotateTransform rotation) return;

        if (brush.IsFrozen || rotation.IsFrozen)
        {
            brush = brush.CloneCurrentValue();
            LiquidBorder.BorderBrush = brush;
            rotation = (RotateTransform)brush.RelativeTransform;
        }

        rotation.BeginAnimation(RotateTransform.AngleProperty,
            showBorder ? new DoubleAnimation(0, 360, TimeSpan.FromSeconds(5))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            } : null);
        LiquidBorder.BeginAnimation(OpacityProperty, charging
            ? new DoubleAnimation(0.68, 1, TimeSpan.FromMilliseconds(850))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            }
            : null);
        if (!charging) LiquidBorder.Opacity = 1;
    }

    private void RefreshChargingBorder()
    {
        var charging = PowerStatusService.IsCharging();
        if (charging == _isCharging) return;
        _isCharging = charging;
        UpdateLiquidBorder(_islandManager.State);
    }

    private static LinearGradientBrush CreateLiquidBorderBrush(bool charging)
    {
        string[] colors = charging
            ? ["#FF154B32", "#FF42D98B", "#FFB8FFD6", "#FF25A862", "#FF103D29", "#FF7BFFC0", "#FF218354", "#FF154B32"]
            : ["#FF30343A", "#FFB9C4CE", "#FFF7FCFF", "#FF71818D", "#FF252A30", "#FFDAE3EB", "#FF56616C", "#FF30343A"];
        double[] offsets = [0, 0.16, 0.23, 0.33, 0.52, 0.72, 0.85, 1];
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            RelativeTransform = new RotateTransform(0, 0.5, 0.5) };
        for (var i = 0; i < colors.Length; i++)
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(colors[i])!, offsets[i]));
        return brush;
    }

    private void AnimateIslandResize(double targetWidth, double targetHeight, IslandState state)
    {
        var version = ++_resizeAnimationVersion;
        var scale = (ScaleTransform)IslandSurface.RenderTransform;
        var currentWidth = Math.Max(1, ActualWidth * scale.ScaleX);
        var currentHeight = Math.Max(1, ActualHeight * scale.ScaleY);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        if (Math.Abs(currentWidth - targetWidth) < 0.5 &&
            Math.Abs(currentHeight - targetHeight) < 0.5 &&
            Math.Abs(ActualWidth - targetWidth) < 0.5 &&
            Math.Abs(ActualHeight - targetHeight) < 0.5)
        {
            FinishResizeAnimation(version, targetWidth, targetHeight);
            return;
        }

        IslandSurface.CornerRadius = state switch
        {
            IslandState.Collapsed => new CornerRadius(13),
            IslandState.Preview => new CornerRadius(22),
            _ => new CornerRadius(32)
        };

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(state switch
        {
            IslandState.Expanded or IslandState.Interaction or IslandState.Notification => 350,
            IslandState.Preview => 270,
            _ => 220
        }));
        var expanding = (targetWidth * targetHeight) >= (currentWidth * currentHeight);

        if (expanding)
        {
            scale.ScaleX = Math.Clamp(currentWidth / targetWidth, 0.05, 1);
            scale.ScaleY = Math.Clamp(currentHeight / targetHeight, 0.05, 1);
            _windowService.ResizeAndPosition(targetWidth, targetHeight);
            var xAnimation = new DoubleAnimation(1, duration) { EasingFunction = easing };
            var yAnimation = new DoubleAnimation(1, duration) { EasingFunction = easing };
            yAnimation.Completed += (_, _) => FinishResizeAnimation(version, targetWidth, targetHeight);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, xAnimation, HandoffBehavior.SnapshotAndReplace);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, yAnimation, HandoffBehavior.SnapshotAndReplace);
            return;
        }

        scale.ScaleX = Math.Clamp(currentWidth / Math.Max(1, ActualWidth), 0.05, 1);
        scale.ScaleY = Math.Clamp(currentHeight / Math.Max(1, ActualHeight), 0.05, 1);
        var targetScaleX = Math.Clamp(targetWidth / Math.Max(1, ActualWidth), 0.05, 1);
        var targetScaleY = Math.Clamp(targetHeight / Math.Max(1, ActualHeight), 0.05, 1);
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
        // Keep the last rendered size while changing the actual window dimensions.
        // Resetting the scale first briefly exposes the full-sized surface.
        if (Math.Abs(ActualWidth - targetWidth) > 0.5 || Math.Abs(ActualHeight - targetHeight) > 0.5)
            _windowService.ResizeAndPosition(targetWidth, targetHeight);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1;
        scale.ScaleY = 1;
        _windowService.RemoveNativeBorder();
        _isAnimating = false;
        if (_islandManager.State == IslandState.Collapsed && !IsMouseOver)
            _suppressHoverUntilPointerLeaves = false;
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

        if (_visibleStateContent == show) return;
        _visibleStateContent = show;

        foreach (var element in new FrameworkElement[] { CollapsedContent, PreviewContent, ExpandedContent })
        {
            if (element == show)
            {
                element.Visibility = Visibility.Visible;
                element.Opacity = 0;
                var isFull = element == ExpandedContent;
                element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1,
                    TimeSpan.FromMilliseconds(isFull ? 240 : 150))
                {
                    BeginTime = TimeSpan.FromMilliseconds(isFull ? 90 : 0)
                });
                var motion = new TranslateTransform();
                element.RenderTransform = motion;
                motion.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(
                    isFull ? -12 : -4, 0, TimeSpan.FromMilliseconds(isFull ? 310 : 170))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                });
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

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        _collapseTimer.Stop();
        _suppressHoverUntilPointerLeaves = true;
        _islandManager.TransitionTo(IslandState.Collapsed);
    }

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
        _ = PasteClipboardContentAsync(false);
    }

    private void ShowPanel(UIElement panel)
    {
        if (_activePanel == panel && panel.Visibility == Visibility.Visible)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateInteractivePanel));
            return;
        }

        if (panel != AppsPanel)
        {
            _interactiveAppPanel.Suspend();
            _liveAppPreview.Suspend();
        }

        var outgoing = _activePanel;
        _activePanel = panel;
        var version = ++_panelAnimationVersion;
        foreach (var other in new UIElement[] { MediaPanel, FilesPanel, ClipboardPanel, SettingsPanel, AppsPanel })
        {
            if (other == panel || other == outgoing) continue;
            other.BeginAnimation(OpacityProperty, null);
            other.IsHitTestVisible = false;
            other.Visibility = Visibility.Collapsed;
        }

        if (outgoing is not null && outgoing != panel && outgoing.Visibility == Visibility.Visible)
        {
            outgoing.IsHitTestVisible = false;
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
            fadeOut.Completed += (_, _) =>
            {
                if (version != _panelAnimationVersion || outgoing == _activePanel) return;
                outgoing.BeginAnimation(OpacityProperty, null);
                outgoing.Visibility = Visibility.Collapsed;
            };
            outgoing.BeginAnimation(OpacityProperty, fadeOut, HandoffBehavior.SnapshotAndReplace);
        }

        panel.BeginAnimation(OpacityProperty, null);
        panel.Visibility = Visibility.Visible;
        panel.IsHitTestVisible = true;
        panel.Opacity = 0;
        var slide = new TranslateTransform();
        panel.RenderTransform = slide;
        slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(18, 0,
            TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(210))
        {
            BeginTime = TimeSpan.FromMilliseconds(35)
        };
        fadeIn.Completed += (_, _) =>
        {
            if (version == _panelAnimationVersion)
                UpdateInteractivePanel();
        };
        panel.BeginAnimation(OpacityProperty, fadeIn, HandoffBehavior.SnapshotAndReplace);
    }

    private void OnMediaChanged(object? sender, MediaSnapshot snapshot) =>
        Dispatcher.InvokeAsync(() => ApplyMediaSnapshot(snapshot));

    private void ApplyMediaSnapshot(MediaSnapshot snapshot)
    {
        var thumbnailChanged = !ReferenceEquals(_currentMediaSnapshot.Thumbnail, snapshot.Thumbnail);
        _currentMediaSnapshot = snapshot;
        TrackTitle.Text = snapshot.Title;
        TrackArtist.Text = snapshot.Artist;
        AlbumTitle.Text = snapshot.Album;
        AlbumTitle.Visibility = string.IsNullOrWhiteSpace(snapshot.Album) ? Visibility.Collapsed : Visibility.Visible;
        PreviewTitle.Text = snapshot.HasSession ? snapshot.Title : "Dynamic Island";
        PreviewArtist.Text = snapshot.HasSession ? snapshot.Artist : "Ready · drag to reposition";
        var playbackIcon = (Geometry)Application.Current.Resources[snapshot.IsPlaying ? "IconPause" : "IconPlay"];
        PlayPauseIcon.Data = playbackIcon;
        PreviewPlayPauseIcon.Data = playbackIcon;
        PreviousButton.IsEnabled = snapshot.CanPrevious;
        PlayPauseButton.IsEnabled = snapshot.CanPlayPause;
        NextButton.IsEnabled = snapshot.CanNext;
        PreviewPreviousButton.IsEnabled = snapshot.CanPrevious;
        PreviewPlayPauseButton.IsEnabled = snapshot.CanPlayPause;
        PreviewNextButton.IsEnabled = snapshot.CanNext;
        OpenPlayerButton.IsEnabled = snapshot.HasSession &&
                                     !string.IsNullOrWhiteSpace(snapshot.SourceAppUserModelId);
        ApplyMediaProgress();
        ApplyPlaybackIndicator();

        if (thumbnailChanged)
        {
            _currentArtwork = CreateImage(snapshot.Thumbnail);
            Artwork.Source = _currentArtwork;
        }
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

    private void ApplyMediaProgress()
    {
        var snapshot = _currentMediaSnapshot;
        var duration = snapshot.EndTime - snapshot.StartTime;
        var hasTimeline = snapshot.HasSession && duration > TimeSpan.Zero;
        SeekSlider.IsEnabled = hasTimeline && snapshot.CanSeek;
        if (_isSeeking) return;

        var elapsed = snapshot.Position - snapshot.StartTime;
        if (snapshot.IsPlaying && snapshot.TimelineUpdatedAt != default)
        {
            var sinceUpdate = DateTimeOffset.UtcNow - snapshot.TimelineUpdatedAt;
            if (sinceUpdate > TimeSpan.Zero)
                elapsed += sinceUpdate;
        }

        var durationSeconds = hasTimeline ? duration.TotalSeconds : 0;
        var elapsedSeconds = Math.Clamp(elapsed.TotalSeconds, 0, durationSeconds);
        _isUpdatingSeek = true;
        try
        {
            SeekSlider.Maximum = Math.Max(1, durationSeconds);
            SeekSlider.Value = elapsedSeconds;
            CurrentTimeText.Text = FormatMediaTime(TimeSpan.FromSeconds(elapsedSeconds));
            DurationText.Text = FormatMediaTime(hasTimeline ? duration : TimeSpan.Zero);
        }
        finally
        {
            _isUpdatingSeek = false;
        }
    }

    private static string FormatMediaTime(TimeSpan time) => time.TotalHours >= 1
        ? time.ToString(@"h\:mm\:ss")
        : time.ToString(@"m\:ss");

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingSeek && CurrentTimeText is not null)
            CurrentTimeText.Text = FormatMediaTime(TimeSpan.FromSeconds(Math.Max(0, e.NewValue)));
    }

    private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _isSeeking = SeekSlider.IsEnabled;

    private void SeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSeeking) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (_isSeeking) _ = CommitSeekAsync();
        }));
    }

    private void SeekSlider_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown)
            _ = CommitSeekAsync();
    }

    private async Task CommitSeekAsync()
    {
        if (!_currentMediaSnapshot.CanSeek)
        {
            _isSeeking = false;
            return;
        }

        var requested = _currentMediaSnapshot.StartTime + TimeSpan.FromSeconds(SeekSlider.Value);
        _isSeeking = false;
        var succeeded = await _mediaService.SeekAsync(requested);
        MediaStatusText.Text = succeeded ? string.Empty : "This player did not accept the seek request.";
        if (!succeeded) ApplyMediaProgress();
    }

    private void OpenPlayerButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MediaAppWindowService.Open(_currentMediaSnapshot.SourceAppUserModelId);
        MediaStatusText.Text = result.Message;
        if (result.Opened)
            _islandManager.TransitionTo(IslandState.Collapsed);
    }

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
        _interactiveAppPanel.Detach();
        _liveAppPreview.Detach();
        _islandManager.TransitionTo(IslandState.Expanded);
        ShowPanel(AppsPanel);
        _ = _virtualDesktopService.MoveToIslandDesktop(window, this);
        AppHostPlaceholder.Visibility = Visibility.Collapsed;
        UpdateLayout();
        if (RequiresLivePreview(window, name))
        {
            if (_liveAppPreview.Attach(window, this, AppHostViewport))
            {
                if (_isAnimating) _liveAppPreview.Suspend();
                AppRunnerStatus.Text = $"Live preview of {name} · use Open externally to interact";
                return;
            }

            ShowAppHostPlaceholder(_liveAppPreview.LastError ?? $"Could not preview {name}");
            return;
        }

        if (_interactiveAppPanel.Attach(window, AppHostViewport))
        {
            if (_isAnimating) _interactiveAppPanel.Suspend();
            AppRunnerStatus.Text = $"Using {name} in the panel";
            return;
        }

        ShowAppHostPlaceholder(_interactiveAppPanel.LastError ?? $"Could not place {name} in interactive panel mode");
    }

    private static bool RequiresLivePreview(nint window, string displayName)
    {
        if (displayName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase))
            return true;

        _ = GetWindowThreadProcessId(window, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

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
        _liveAppPreview.Detach();
        ShowAppHostPlaceholder("The hosted application was detached");
    }

    private void ShowAppHostPlaceholder(string status)
    {
        _interactiveAppPanel.Detach();
        _liveAppPreview.Detach();
        AppHostPlaceholder.Visibility = Visibility.Visible;
        AppRunnerStatus.Text = status;
    }

    private void UpdateInteractivePanel()
    {
        if (_isAnimating || WindowState == WindowState.Minimized) return;
        if (_interactiveAppPanel.IsAttached)
        {
            if (_islandManager.State == IslandState.Expanded && _activePanel == AppsPanel &&
                AppsPanel.Visibility == Visibility.Visible)
            {
                _interactiveAppPanel.Resume(AppHostViewport);
            }
            else
            {
                _interactiveAppPanel.Suspend();
            }
        }
        if (_liveAppPreview.IsAttached)
        {
            if (_islandManager.State == IslandState.Expanded && _activePanel == AppsPanel &&
                AppsPanel.Visibility == Visibility.Visible)
                _liveAppPreview.Resume(AppHostViewport);
            else
                _liveAppPreview.Suspend();
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

    private async void PasteTextButton_Click(object sender, RoutedEventArgs e) =>
        await PasteClipboardContentAsync(true);

    private async Task PasteClipboardContentAsync(bool showEmptyMessage)
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var paths = Clipboard.GetFileDropList().Cast<string>()
                    .Where(File.Exists).ToList();
                if (paths.Count > 0)
                {
                    ShowClipboardFiles(paths);
                    return;
                }
            }

            // Explorer and some modern apps expose copied documents as StorageItems.
            List<string> modernPaths;
            try
            {
                var storageFiles = await _clipboardService.GetFilesAsync();
                modernPaths = storageFiles.Select(file => file.Path).Where(File.Exists).ToList();
            }
            catch
            {
                modernPaths = [];
            }
            if (modernPaths.Count > 0)
            {
                ShowClipboardFiles(modernPaths);
                return;
            }

            if (Clipboard.ContainsImage())
            {
                _clipboardImage = Clipboard.GetImage();
                ClipboardImagePreview.Source = _clipboardImage;
                SetClipboardKind(ClipboardKind.Image);
                ClipboardStatus.Text = "Image ready to copy";
                return;
            }

            if (Clipboard.ContainsText())
            {
                ClipboardTextBox.Text = Clipboard.GetText();
                SetClipboardKind(ClipboardKind.Text);
                ClipboardStatus.Text = "Pasted text from Windows clipboard";
                return;
            }

            if (showEmptyMessage) ClipboardStatus.Text = "No supported clipboard content found";
        }
        catch (Exception exception)
        {
            ClipboardStatus.Text = $"Could not read clipboard: {exception.Message}";
        }
    }

    private void ShowClipboardFiles(List<string> paths)
    {
        _clipboardFiles = paths;
        ClipboardFilesList.ItemsSource = paths.Select(Path.GetFileName).ToList();
        SetClipboardKind(ClipboardKind.Files);
        ClipboardStatus.Text = $"{paths.Count} file{(paths.Count == 1 ? "" : "s")} ready to copy";
    }

    private void SetClipboardKind(ClipboardKind kind)
    {
        _clipboardKind = kind;
        ClipboardTextBox.Visibility = kind == ClipboardKind.Text ? Visibility.Visible : Visibility.Collapsed;
        ClipboardImagePanel.Visibility = kind == ClipboardKind.Image ? Visibility.Visible : Visibility.Collapsed;
        ClipboardFilesPanel.Visibility = kind == ClipboardKind.Files ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EditClipboardTextButton_Click(object sender, RoutedEventArgs e)
    {
        SetClipboardKind(ClipboardKind.Text);
        ClipboardTextBox.Focus();
        ClipboardStatus.Text = "Edit text, then choose Copy";
    }

    private void CopyTextButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            switch (_clipboardKind)
            {
                case ClipboardKind.Image when _clipboardImage is not null:
                    Clipboard.SetImage(_clipboardImage);
                    ClipboardStatus.Text = "Copied image to Windows clipboard";
                    break;
                case ClipboardKind.Files when _clipboardFiles.Count > 0:
                    var existing = _clipboardFiles.Where(File.Exists).ToList();
                    if (existing.Count == 0)
                    {
                        ClipboardStatus.Text = "These files are no longer available";
                        break;
                    }
                    var paths = new StringCollection();
                    paths.AddRange(existing.ToArray());
                    Clipboard.SetFileDropList(paths);
                    ClipboardStatus.Text = $"Copied {existing.Count} file{(existing.Count == 1 ? "" : "s")} to Windows clipboard";
                    break;
                default:
                    Clipboard.SetText(ClipboardTextBox.Text);
                    ClipboardStatus.Text = "Copied text to Windows clipboard";
                    break;
            }
        }
        catch (Exception exception)
        {
            ClipboardStatus.Text = $"Could not copy: {exception.Message}";
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var opacity = Math.Clamp(e.NewValue / 100, 0.1, 1);
        ApplySurfaceOpacity(opacity);
        if (!_settingsLoaded) return;
        _configuration.SurfaceOpacity = opacity;
        QueueSettingsSave();
    }

    private void OpacitySlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (OpacitySlider.Template.FindName("OpacityThumb", OpacitySlider) is not Thumb thumb ||
            thumb.RenderTransform is not ScaleTransform scale) return;

        // Template-created Freezables may be shared and frozen; animate a local copy.
        if (scale.IsFrozen)
        {
            scale = scale.CloneCurrentValue();
            thumb.RenderTransform = scale;
        }

        var bounce = new DoubleAnimationUsingKeyFrames();
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0.78, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(1.13, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
    }

    private void ApplySurfaceOpacity(double opacity)
    {
        var alpha = (byte)Math.Round(255 * opacity);
        if (_configuration.SurfaceStyle == IslandSurfaceStyle.Gradient &&
            TryParseSurfaceColor(_configuration.GradientStartColor, out var startColor) &&
            TryParseSurfaceColor(_configuration.GradientEndColor, out var endColor))
        {
            var (start, end) = _configuration.GradientDirection switch
            {
                IslandGradientDirection.LeftToRight => (new Point(0, 0.5), new Point(1, 0.5)),
                IslandGradientDirection.Diagonal => (new Point(0, 0), new Point(1, 1)),
                _ => (new Point(0.5, 0), new Point(0.5, 1))
            };
            IslandSurface.Background = new LinearGradientBrush(
                Color.FromArgb(alpha, startColor.R, startColor.G, startColor.B),
                Color.FromArgb(alpha, endColor.R, endColor.G, endColor.B), start, end);
            OpacityValueText.Text = $"{opacity:P0}";
            return;
        }

        var baseColor = _configuration.Theme == IslandTheme.Light
            ? Color.FromRgb(246, 246, 248)
            : Color.FromRgb(17, 17, 19);
        IslandSurface.Background = new SolidColorBrush(Color.FromArgb(
            alpha, baseColor.R, baseColor.G, baseColor.B));
        OpacityValueText.Text = $"{opacity:P0}";
    }

    private static bool TryParseSurfaceColor(string? value, out Color color)
    {
        color = default;
        if (value is null || value.Length is not (7 or 9) || value[0] != '#' ||
            value.Skip(1).Any(character => !Uri.IsHexDigit(character)))
            return false;

        color = (Color)ColorConverter.ConvertFromString(value);
        return true;
    }

    private void SurfaceStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsLoaded || SurfaceStyleComboBox.SelectedIndex < 0) return;
        _configuration.SurfaceStyle = (IslandSurfaceStyle)SurfaceStyleComboBox.SelectedIndex;
        GradientOptions.Visibility = _configuration.SurfaceStyle == IslandSurfaceStyle.Gradient
            ? Visibility.Visible : Visibility.Collapsed;
        ApplySurfaceOpacity(_configuration.SurfaceOpacity);
        QueueSettingsSave();
    }

    private void GradientDirectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsLoaded || GradientDirectionComboBox.SelectedIndex < 0) return;
        _configuration.GradientDirection = (IslandGradientDirection)GradientDirectionComboBox.SelectedIndex;
        ApplySurfaceOpacity(_configuration.SurfaceOpacity);
        QueueSettingsSave();
    }

    private void PickGradientColorButton_Click(object sender, RoutedEventArgs e)
    {
        var target = (sender as FrameworkElement)?.Tag as string == "Start"
            ? GradientStartColorTextBox : GradientEndColorTextBox;
        var initial = TryParseSurfaceColor(target.Text, out var color)
            ? color : Color.FromRgb(110, 214, 169);
        if (!ColorPickerService.TryPick(this, initial, out var selected)) return;
        target.Text = $"#{selected.R:X2}{selected.G:X2}{selected.B:X2}";
        ApplyGradientButton_Click(sender, e);
    }

    private void ApplyGradientButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseSurfaceColor(GradientStartColorTextBox.Text, out var start) ||
            !TryParseSurfaceColor(GradientEndColorTextBox.Text, out var end))
        {
            GradientStatusText.Text = "Enter two hex colors such as #334654 and #161821.";
            return;
        }

        _configuration.GradientStartColor = $"#FF{start.R:X2}{start.G:X2}{start.B:X2}";
        _configuration.GradientEndColor = $"#FF{end.R:X2}{end.G:X2}{end.B:X2}";
        GradientStartColorTextBox.Text = _configuration.GradientStartColor;
        GradientEndColorTextBox.Text = _configuration.GradientEndColor;
        GradientStatusText.Text = "Gradient updated.";
        ApplySurfaceOpacity(_configuration.SurfaceOpacity);
        QueueSettingsSave();
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
        var miniImage = _customMiniLogo ?? (useBrand ? _brandLogo : useArtwork ? _currentArtwork : null);
        var useMusicGlyph = _configuration.LogoStyle is IslandLogoStyle.MusicNote or IslandLogoStyle.AlbumArtwork;
        MiniLogoArtwork.Source = miniImage;
        MiniLogoArtwork.Visibility = miniImage is not null ? Visibility.Visible : Visibility.Collapsed;
        MiniLogoGlyph.Visibility = miniImage is null && useMusicGlyph ? Visibility.Visible : Visibility.Collapsed;
        MiniLogoText.Visibility = miniImage is null && !useMusicGlyph ? Visibility.Visible : Visibility.Collapsed;
        PreviewArtwork.Source = useBrand ? _brandLogo : useArtwork ? _currentArtwork : null;
        PreviewArtwork.Visibility = showImage ? Visibility.Visible : Visibility.Collapsed;
        PreviewLogoGlyph.Visibility = !showImage && useMusicGlyph ? Visibility.Visible : Visibility.Collapsed;
        PreviewFallbackIcon.Visibility = showImage || useMusicGlyph ? Visibility.Collapsed : Visibility.Visible;
        MiniLogoContainer.Background = _customMiniLogo is not null
            ? Brushes.Transparent
            : useBrand
            ? new SolidColorBrush(Color.FromRgb(26, 28, 31))
            : (Brush)Application.Current.Resources["PrimaryActionBrush"];
        MiniLogoContainer.Width = _customMiniLogo is not null ? 18 : useBrand ? 22 : 16;
        MiniLogoContainer.Height = _customMiniLogo is not null ? 18 : 16;
        MiniLogoContainer.CornerRadius = new CornerRadius(_customMiniLogo is not null ? 9 : 8);
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

    private void ChooseMiniLogoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a picture for the minimized island",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*"
        };
        _isLogoEditorOpen = true;
        _collapseTimer.Stop();
        try
        {
            if (dialog.ShowDialog(this) != true) return;
            var source = _miniLogoStorage.LoadSource(dialog.FileName);
            var editor = new MiniLogoEditorWindow(source) { Owner = this };
            if (editor.ShowDialog() != true || editor.EditedImage is null) return;

            _customMiniLogo = _miniLogoStorage.Save(editor.EditedImage);
            _configuration.MiniLogoImagePath = _miniLogoStorage.StoredPath;
            MiniLogoStatusText.Text = "Edited picture saved for the minimized island.";
            ApplyLogoStyle();
            QueueSettingsSave();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            MiniLogoStatusText.Text = $"Could not use this picture: {exception.Message}";
        }
        finally
        {
            _isLogoEditorOpen = false;
        }
    }

    private void ClearMiniLogoButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _miniLogoStorage.Clear();
            _customMiniLogo = null;
            _configuration.MiniLogoImagePath = null;
            MiniLogoStatusText.Text = "Using the selected island logo.";
            ApplyLogoStyle();
            QueueSettingsSave();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MiniLogoStatusText.Text = $"Could not remove the saved picture: {exception.Message}";
        }
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
        _mediaProgressTimer.Stop();
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
        try
        {
            await _configurationService.SaveAsync(_configuration);
            SettingsSaveStatusText.Text = "Changes are saved automatically.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A protected or read-only profile must not prevent the island from opening.
            // Keep the current settings in memory and make the persistence failure visible.
            System.Diagnostics.Debug.WriteLine($"Could not save Dynamic Island settings: {exception}");
            SettingsSaveStatusText.Text = "Settings could not be saved in this Windows profile; changes may be lost when the island exits.";
            IslandSurface.ToolTip = SettingsSaveStatusText.Text;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _topmostTimer.Stop();
        _powerTimer.Stop();
        _collapseTimer.Stop();
        _settingsSaveTimer.Stop();
        _interactiveAppPanel.Detach();
        _liveAppPreview.Detach();

        _mediaService.Dispose();
    }
}
