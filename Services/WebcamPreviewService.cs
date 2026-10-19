using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace DynamicIsland.Services;

public sealed class WebcamPreviewService : IAsyncDisposable
{
    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private int _processingFrame;
    private long _lastFrameTick;

    public event EventHandler<byte[]>? FrameReady;
    public string? LastError { get; private set; }
    public bool IsRunning => _reader is not null;

    public async Task<bool> StartAsync()
    {
        if (_reader is not null) return true;
        try
        {
            _capture = new MediaCapture();
            await _capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            });

            var source = _capture.FrameSources.Values.FirstOrDefault(item =>
                item.Info.SourceKind == MediaFrameSourceKind.Color);
            if (source is null)
            {
                LastError = "No webcam was found.";
                await StopAsync();
                return false;
            }

            _reader = await _capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
            _reader.FrameArrived += Reader_FrameArrived;
            var status = await _reader.StartAsync();
            if (status != MediaFrameReaderStartStatus.Success)
            {
                LastError = $"The webcam could not start ({status}).";
                await StopAsync();
                return false;
            }

            LastError = null;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LastError = $"Webcam unavailable: {exception.Message}";
            await StopAsync();
            return false;
        }
    }

    private async void Reader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastFrameTick) < 120 ||
            Interlocked.Exchange(ref _processingFrame, 1) != 0)
            return;

        Interlocked.Exchange(ref _lastFrameTick, now);
        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            using var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (bitmap is null) return;

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetSoftwareBitmap(bitmap);
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
            await encoder.FlushAsync();

            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            FrameReady?.Invoke(this, bytes);
        }
        catch
        {
            // Dropping one camera frame is preferable to interrupting the island.
        }
        finally
        {
            Interlocked.Exchange(ref _processingFrame, 0);
        }
    }

    public async Task StopAsync()
    {
        var reader = _reader;
        _reader = null;
        if (reader is not null)
        {
            reader.FrameArrived -= Reader_FrameArrived;
            try { await reader.StopAsync(); } catch { }
            reader.Dispose();
        }

        _capture?.Dispose();
        _capture = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
