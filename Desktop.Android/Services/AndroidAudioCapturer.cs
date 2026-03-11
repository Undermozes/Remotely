using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Audio capturer stub for Android.
/// Full audio capture requires the CAPTURE_AUDIO_OUTPUT privileged permission,
/// which is only available to system apps or via ADB grant.
/// This stub is a safe no-op; a future iteration can enable capture when the
/// permission has been granted.
/// </summary>
public class AndroidAudioCapturer : IAudioCapturer
{
    private readonly ILogger<AndroidAudioCapturer> _logger;

#pragma warning disable CS0067
    public event EventHandler<byte[]>? AudioSampleReady;
#pragma warning restore CS0067

    public AndroidAudioCapturer(ILogger<AndroidAudioCapturer> logger)
    {
        _logger = logger;
    }

    public void ToggleAudio(bool toggleOn)
    {
        _logger.LogInformation(
            "Audio capture requested (toggleOn={toggleOn}), but it is not enabled without CAPTURE_AUDIO_OUTPUT permission.",
            toggleOn);
    }
}
