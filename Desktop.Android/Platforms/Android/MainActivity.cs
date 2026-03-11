using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media.Projection;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using Remotely.Desktop.Android.Services;
using Remotely.Desktop.Shared.Abstractions;

namespace Remotely.Desktop.Android;

[Activity(
    Label = "Remotely",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public class MainActivity : Activity
{
    // Result code for the MediaProjection permission request.
    private const int MediaProjectionRequestCode = 100;

    private IAndroidScreenCapturer? _screenCapturer;

    /// <summary>
    /// Gets the current active <see cref="MainActivity"/> instance, or null if not started.
    /// Used by dialog-based services (chat, access prompts).
    /// </summary>
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Current = this;

        _screenCapturer = MainApplication.Services?
            .GetService<IAndroidScreenCapturer>();

        // Start the agent foreground service.
        var serviceIntent = new Intent(this, typeof(AgentForegroundService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            StartForegroundService(serviceIntent);
        }
        else
        {
            StartService(serviceIntent);
        }

        // Request screen capture permission if not already granted.
        RequestMediaProjectionPermission();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Current == this)
        {
            Current = null;
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == MediaProjectionRequestCode)
        {
            if (resultCode == Result.Ok && data != null)
            {
                _screenCapturer?.OnMediaProjectionGranted((int)resultCode, data);
            }
        }
    }

    private void RequestMediaProjectionPermission()
    {
        if (_screenCapturer?.IsProjectionGranted == true)
        {
            return;
        }

        var mediaProjectionManager = (MediaProjectionManager?)
            GetSystemService(MediaProjectionService);

        if (mediaProjectionManager != null)
        {
            StartActivityForResult(
                mediaProjectionManager.CreateScreenCaptureIntent(),
                MediaProjectionRequestCode);
        }
    }
}
