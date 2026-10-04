using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland.Services;

public sealed class MiniLogoStorageService
{
    private readonly string _storedPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIsland", "Assets", "mini-logo.png");

    public string StoredPath => _storedPath;

    public BitmapSource? Load(string? configuredPath)
    {
        if (!string.Equals(configuredPath, _storedPath, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(_storedPath))
            return null;

        using var stream = File.OpenRead(_storedPath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var image = decoder.Frames[0];
        image.Freeze();
        return image;
    }

    public BitmapSource Save(string sourcePath)
    {
        return Save(LoadSource(sourcePath));
    }

    public BitmapSource LoadSource(string sourcePath)
    {
        var source = new FileInfo(sourcePath);
        if (!source.Exists || source.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Choose an image smaller than 16 MB.");

        using var stream = File.OpenRead(source.FullName);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        ValidateDimensions(frame);
        frame.Freeze();
        return frame;
    }

    public BitmapSource Save(BitmapSource source)
    {
        ValidateDimensions(source);
        var frame = source;
        var scale = Math.Min(1.0, 128.0 / Math.Max(frame.PixelWidth, frame.PixelHeight));
        BitmapSource image = scale < 1
            ? new TransformedBitmap(frame, new ScaleTransform(scale, scale))
            : frame;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        Directory.CreateDirectory(Path.GetDirectoryName(_storedPath)!);
        var temporaryPath = _storedPath + ".tmp";
        try
        {
            using (var target = File.Create(temporaryPath))
                encoder.Save(target);
            File.Move(temporaryPath, _storedPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return Load(_storedPath)!;
    }

    private static void ValidateDimensions(BitmapSource frame)
    {
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 ||
            frame.PixelWidth > 8192 || frame.PixelHeight > 8192 ||
            (long)frame.PixelWidth * frame.PixelHeight > 32_000_000)
            throw new InvalidDataException("Choose an image smaller than 32 megapixels.");
    }

    public void Clear()
    {
        if (File.Exists(_storedPath))
            File.Delete(_storedPath);
    }
}
