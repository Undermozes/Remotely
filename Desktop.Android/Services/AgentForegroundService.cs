using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Services;
using Remotely.Shared.Services;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Android foreground service that keeps the Remotely agent alive while the app
/// is in the background.  It maintains the SignalR connection to the Remotely server,
/// reports device info, and responds to remote commands.
/// </summary>
[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaProjection)]
public class AgentForegroundService : Service
{
    private const string ChannelId = "remotely_agent";
    private const int ForegroundNotificationId = 1000;

    private IDesktopHubConnection? _hubConnection;
    private ILogger<AgentForegroundService>? _logger;
    private CancellationTokenSource _cts = new();

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();

        _hubConnection = MainApplication.Services?.GetService<IDesktopHubConnection>();
        _logger = MainApplication.Services?.GetService<ILogger<AgentForegroundService>>();

        CreateNotificationChannel();
        StartForeground(ForegroundNotificationId, BuildNotification());
    }

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ConnectAndRunAsync(_cts.Token));
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _cts.Cancel();
        base.OnDestroy();
    }

    private async Task ConnectAndRunAsync(CancellationToken cancelToken)
    {
        try
        {
            if (_hubConnection is null)
            {
                _logger?.LogError("Hub connection service not available.");
                return;
            }

            _logger?.LogInformation("Connecting to Remotely server...");

            var connected = await _hubConnection.Connect(
                TimeSpan.FromSeconds(30),
                cancelToken);

            if (!connected)
            {
                _logger?.LogWarning("Failed to connect to server. Retrying in 30 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(30), cancelToken);
                await ConnectAndRunAsync(cancelToken);
                return;
            }

            _logger?.LogInformation("Connected to Remotely server.");

            // Keep the connection alive until cancellation.
            while (!cancelToken.IsCancellationRequested
                && _hubConnection.IsConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancelToken);
            }
        }
        catch (System.OperationCanceledException)
        {
            _logger?.LogInformation("Agent foreground service cancelled.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in agent foreground service.");
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var notificationManager = (NotificationManager?)
            GetSystemService(NotificationService);

        if (notificationManager?.GetNotificationChannel(ChannelId) != null)
        {
            return;
        }

        var channel = new NotificationChannel(
            ChannelId,
            "Remotely Agent",
            NotificationImportance.Low)
        {
            Description = "Keeps the Remotely agent connected in the background."
        };

        notificationManager?.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop);
        var pendingIntent = PendingIntent.GetActivity(
            this,
            0,
            intent,
            PendingIntentFlags.Immutable);

        return new Notification.Builder(this, ChannelId)
            .SetContentTitle("Remotely Agent")
            .SetContentText("Running in background.")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMyCalendar)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .Build()!;
    }
}
