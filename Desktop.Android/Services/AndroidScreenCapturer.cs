using Android.Content;
using Android.Graphics;
using Android.Hardware.Display;
using Android.Media;
using Android.Media.Projection;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Services;
using SkiaSharp;
using System.Drawing;
using RemotelyResult = Remotely.Shared.Primitives.Result;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Extended interface to expose Android-specific MediaProjection grant handling.
/// </summary>
public interface IAndroidScreenCapturer : IScreenCapturer
{
    bool IsProjectionGranted { get; }
    void OnMediaProjectionGranted(int resultCode, Intent data);
}

/// <summary>
/// Captures the Android device screen using the MediaProjection API (API 21+).
/// The user must grant the screen-capture permission via the system dialog (shown
/// by <see cref="MainActivity"/>) before capture can begin.
/// </summary>
public class AndroidScreenCapturer : IAndroidScreenCapturer
{
    private const string DisplayName = "display0";

    private readonly Context _context;
    private readonly IImageHelper _imageHelper;
    private readonly ILogger<AndroidScreenCapturer> _logger;
    private readonly object _frameLock = new();

    private MediaProjectionManager? _projectionManager;
    private MediaProjection? _mediaProjection;
    private VirtualDisplay? _virtualDisplay;
    private ImageReader? _imageReader;
    private SKBitmap? _currentFrame;
    private SKBitmap? _previousFrame;
    private bool _needsInit = true;

    public AndroidScreenCapturer(
        Context context,
        IImageHelper imageHelper,
        ILogger<AndroidScreenCapturer> logger)
    {
        _context = context;
        _imageHelper = imageHelper;
        _logger = logger;

        _projectionManager = (MediaProjectionManager?)
            _context.GetSystemService(Context.MediaProjectionService);
    }

    public event EventHandler<System.Drawing.Rectangle>? ScreenChanged;

    public bool CaptureFullscreen { get; set; } = true;
    public System.Drawing.Rectangle CurrentScreenBounds { get; private set; }
    public bool IsGpuAccelerated => false;
    public bool IsProjectionGranted => _mediaProjection != null;
    public string SelectedScreen { get; private set; } = DisplayName;

    public void Dispose()
    {
        try
        {
            _virtualDisplay?.Release();
            _imageReader?.Close();
            _mediaProjection?.Stop();
            _currentFrame?.Dispose();
            _previousFrame?.Dispose();
            GC.SuppressFinalize(this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing AndroidScreenCapturer.");
        }
    }

    public IEnumerable<string> GetDisplayNames()
    {
        yield return DisplayName;
    }

    public SKRect GetFrameDiffArea()
    {
        if (_currentFrame is null)
        {
            return SKRect.Empty;
        }
        return _imageHelper.GetDiffArea(_currentFrame, _previousFrame, CaptureFullscreen);
    }

    public Remotely.Shared.Primitives.Result<SKBitmap> GetImageDiff()
    {
        if (_currentFrame is null)
        {
            return RemotelyResult.Fail<SKBitmap>("Current frame is null.");
        }
        return _imageHelper.GetImageDiff(_currentFrame, _previousFrame);
    }

    public Remotely.Shared.Primitives.Result<SKBitmap> GetNextFrame()
    {
        lock (_frameLock)
        {
            try
            {
                if (_mediaProjection is null)
                {
                    return RemotelyResult.Fail<SKBitmap>("MediaProjection not granted yet.");
                }

                if (_needsInit)
                {
                    Init();
                }

                if (_imageReader is null)
                {
                    return RemotelyResult.Fail<SKBitmap>("ImageReader not initialized.");
                }

                using var image = _imageReader.AcquireLatestImage();
                if (image is null)
                {
                    return RemotelyResult.Fail<SKBitmap>("No new frame available.");
                }

                var planes = image.GetPlanes();
                if (planes is null || planes.Length == 0)
                {
                    return RemotelyResult.Fail<SKBitmap>("Image planes are empty.");
                }

                var plane = planes[0];
                var buffer = plane.Buffer;
                if (buffer is null)
                {
                    return RemotelyResult.Fail<SKBitmap>("Plane buffer is null.");
                }

                var width = image.Width;
                var height = image.Height;
                var pixelStride = plane.PixelStride;
                var rowStride = plane.RowStride;

                if (_currentFrame != null)
                {
                    _previousFrame?.Dispose();
                    _previousFrame = _currentFrame;
                }

                var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

                unsafe
                {
                    var destPtr = (byte*)bitmap.GetPixels().ToPointer();
                    var bytes = new byte[buffer.Remaining()];
                    buffer.Get(bytes);

                    for (var row = 0; row < height; row++)
                    {
                        for (var col = 0; col < width; col++)
                        {
                            var srcIndex = row * rowStride + col * pixelStride;
                            var destIndex = (row * width + col) * 4;
                            // RGBA → BGRA conversion
                            destPtr[destIndex + 0] = bytes[srcIndex + 2]; // B
                            destPtr[destIndex + 1] = bytes[srcIndex + 1]; // G
                            destPtr[destIndex + 2] = bytes[srcIndex + 0]; // R
                            destPtr[destIndex + 3] = bytes[srcIndex + 3]; // A
                        }
                    }
                }

                _currentFrame = bitmap;
                return RemotelyResult.Ok(_currentFrame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting next frame.");
                _needsInit = true;
                return RemotelyResult.Fail<SKBitmap>(ex);
            }
        }
    }

    public int GetScreenCount() => 1;

    public System.Drawing.Rectangle GetVirtualScreenBounds() => CurrentScreenBounds;

    public void Init()
    {
        try
        {
            var displayMetrics = _context.Resources?.DisplayMetrics;
            if (displayMetrics is null)
            {
                _logger.LogError("Failed to get display metrics.");
                return;
            }

            var width = displayMetrics.WidthPixels;
            var height = displayMetrics.HeightPixels;
            var dpi = (int)displayMetrics.DensityDpi;

            CurrentScreenBounds = new System.Drawing.Rectangle(0, 0, width, height);
            CaptureFullscreen = true;

            _imageReader?.Close();
            _virtualDisplay?.Release();

            // PixelFormat.RGBA_8888 = 1 (used for screen capture with MediaProjection)
            _imageReader = ImageReader.NewInstance(width, height, (ImageFormatType)1, 2);

            // MediaProjection.CreateVirtualDisplay signature:
            // (name, width, height, dpi, DisplayFlags flags, Surface surface, Callback callback, Handler handler)
            _virtualDisplay = _mediaProjection?.CreateVirtualDisplay(
                "RemotelyCapture",
                width,
                height,
                dpi,
                DisplayFlags.None,
                _imageReader.Surface,
                null,
                null);

            _needsInit = false;
            ScreenChanged?.Invoke(this, CurrentScreenBounds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while initializing screen capturer.");
        }
    }

    public void OnMediaProjectionGranted(int resultCode, Intent data)
    {
        _mediaProjection = _projectionManager?.GetMediaProjection(resultCode, data);
        _needsInit = true;
        _logger.LogInformation("MediaProjection granted.");
    }

    public void SetSelectedScreen(string displayName)
    {
        // Android devices typically have a single display.
        // Multi-display support can be added in a future iteration.
        SelectedScreen = DisplayName;
    }
}
