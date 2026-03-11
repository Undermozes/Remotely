using Android.Content;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Provides clipboard read/write access on Android using <see cref="ClipboardManager"/>.
/// Change notifications are polled on a background task because Android's
/// <see cref="ClipboardManager.IOnPrimaryClipChangedListener"/> is not accessible from
/// a background service without a looper.
/// </summary>
public class AndroidClipboardService : IClipboardService
{
    private readonly Context _context;
    private readonly ILogger<AndroidClipboardService> _logger;
    private ClipboardManager? _clipboardManager;
    private string _lastClipboardText = string.Empty;
    private Task? _watcherTask;
    private CancellationTokenSource _cts = new();

    public event EventHandler<string>? ClipboardTextChanged;

    public AndroidClipboardService(
        Context context,
        ILogger<AndroidClipboardService> logger)
    {
        _context = context;
        _logger = logger;
        _clipboardManager = (ClipboardManager?)_context.GetSystemService(Context.ClipboardService);
    }

    public void BeginWatching()
    {
        if (_watcherTask is { Status: System.Threading.Tasks.TaskStatus.Running })
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _watcherTask = Task.Run(() => WatchClipboard(_cts.Token), _cts.Token);
    }

    public async Task SetText(string clipboardText)
    {
        try
        {
            var clip = ClipData.NewPlainText("Remotely", clipboardText);
            if (clip != null)
            {
                _clipboardManager!.PrimaryClip = clip;
            }
            _lastClipboardText = clipboardText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting clipboard text.");
        }
        await Task.CompletedTask;
    }

    private async Task WatchClipboard(CancellationToken cancelToken)
    {
        while (!cancelToken.IsCancellationRequested)
        {
            try
            {
                if (_clipboardManager?.HasPrimaryClip == true)
                {
                    var item = _clipboardManager.PrimaryClip?.GetItemAt(0);
                    var text = item?.Text;
                    if (!string.IsNullOrEmpty(text) && text != _lastClipboardText)
                    {
                        _lastClipboardText = text;
                        ClipboardTextChanged?.Invoke(this, text);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while watching clipboard.");
            }
            await Task.Delay(500, cancelToken);
        }
    }
}
