using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland.UI;

public partial class MiniLogoEditorWindow : Window
{
    private BitmapSource _source;
    private Rect _imageBounds;
    private Rect _selection;
    private Point? _dragStart;
    private Rect _selectionBeforeDrag;

    public BitmapSource? EditedImage { get; private set; }

    public MiniLogoEditorWindow(BitmapSource source)
    {
        InitializeComponent();
        _source = source;
        SourceImage.Source = source;
        Loaded += (_, _) => ResetSelection();
    }

    private void RotateLeft_Click(object sender, RoutedEventArgs e) => Rotate(-90);

    private void RotateRight_Click(object sender, RoutedEventArgs e) => Rotate(90);

    private void Rotate(int degrees)
    {
        var rotated = new TransformedBitmap(_source, new RotateTransform(degrees));
        rotated.Freeze();
        _source = rotated;
        SourceImage.Source = rotated;
        ResetSelection();
    }

    private void ResetSelection_Click(object sender, RoutedEventArgs e) => ResetSelection();

    private void ResetSelection()
    {
        var scale = Math.Min(CropCanvas.Width / _source.PixelWidth,
                             CropCanvas.Height / _source.PixelHeight);
        var width = _source.PixelWidth * scale;
        var height = _source.PixelHeight * scale;
        _imageBounds = new Rect((CropCanvas.Width - width) / 2,
                                (CropCanvas.Height - height) / 2, width, height);
        _selection = _imageBounds;
        RenderSelection();
        UpdatePreview();
    }

    private void CropCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(CropCanvas);
        if (!_imageBounds.Contains(point)) return;
        _selectionBeforeDrag = _selection;
        _dragStart = point;
        CropCanvas.CaptureMouse();
        _selection = new Rect(point, point);
        RenderSelection();
        e.Handled = true;
    }

    private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed) return;
        var point = ClampToImage(e.GetPosition(CropCanvas));
        _selection = new Rect(_dragStart.Value, point);
        RenderSelection();
    }

    private void CropCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null) return;
        CropCanvas.ReleaseMouseCapture();
        _dragStart = null;
        if (_selection.Width < 6 || _selection.Height < 6)
            _selection = _selectionBeforeDrag;
        RenderSelection();
        UpdatePreview();
        e.Handled = true;
    }

    private Point ClampToImage(Point point) => new(
        Math.Clamp(point.X, _imageBounds.Left, _imageBounds.Right),
        Math.Clamp(point.Y, _imageBounds.Top, _imageBounds.Bottom));

    private void RenderSelection()
    {
        Canvas.SetLeft(SelectionRectangle, _selection.Left);
        Canvas.SetTop(SelectionRectangle, _selection.Top);
        SelectionRectangle.Width = Math.Max(1, _selection.Width);
        SelectionRectangle.Height = Math.Max(1, _selection.Height);
    }

    private void UpdatePreview() => CropPreview.Source = CreateCrop();

    private BitmapSource CreateCrop()
    {
        var crop = _selection.Width < 1 || _selection.Height < 1 ? _imageBounds : _selection;
        var x = (int)Math.Floor((crop.Left - _imageBounds.Left) * _source.PixelWidth / _imageBounds.Width);
        var y = (int)Math.Floor((crop.Top - _imageBounds.Top) * _source.PixelHeight / _imageBounds.Height);
        var right = (int)Math.Ceiling((crop.Right - _imageBounds.Left) * _source.PixelWidth / _imageBounds.Width);
        var bottom = (int)Math.Ceiling((crop.Bottom - _imageBounds.Top) * _source.PixelHeight / _imageBounds.Height);
        x = Math.Clamp(x, 0, _source.PixelWidth - 1);
        y = Math.Clamp(y, 0, _source.PixelHeight - 1);
        right = Math.Clamp(right, x + 1, _source.PixelWidth);
        bottom = Math.Clamp(bottom, y + 1, _source.PixelHeight);
        var result = new CroppedBitmap(_source, new Int32Rect(x, y, right - x, bottom - y));
        result.Freeze();
        return result;
    }

    private void UseCrop_Click(object sender, RoutedEventArgs e)
    {
        EditedImage = CreateCrop();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
