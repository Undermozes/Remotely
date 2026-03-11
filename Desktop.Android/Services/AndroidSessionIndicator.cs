using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Displays a persistent foreground notification to inform the device owner
/// that a Remotely remote control session is active.
/// </summary>
public class AndroidSessionIndicator : ISessionIndicator
{
    private const string ChannelId = "remotely_session";
    private const int NotificationId = 1001;

    private readonly Context _context;
    private readonly ILogger<AndroidSessionIndicator> _logger;

    public AndroidSessionIndicator(
        Context context,
        ILogger<AndroidSessionIndicator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void Show()
    {
        try
        {
            EnsureNotificationChannel();

            var notificationManager = (NotificationManager?)
                _context.GetSystemService(Context.NotificationService);

            // Intent to stop the session: opens MainActivity.
            var intent = new Intent(_context, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.SingleTop);
            var pendingIntent = PendingIntent.GetActivity(
                _context,
                0,
                intent,
                PendingIntentFlags.Immutable);

            var notification = new Notification.Builder(_context, ChannelId)
                .SetContentTitle("Remotely – Session Active")
                .SetContentText("Your screen is being accessed remotely. Tap to view.")
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .Build();

            notificationManager?.Notify(NotificationId, notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while showing session indicator.");
        }
    }

    private void EnsureNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var notificationManager = (NotificationManager?)
            _context.GetSystemService(Context.NotificationService);

        if (notificationManager?.GetNotificationChannel(ChannelId) != null)
        {
            return;
        }

        var channel = new NotificationChannel(
            ChannelId,
            "Remotely Session",
            NotificationImportance.Low)
        {
            Description = "Shown while a Remotely remote control session is active."
        };

        notificationManager?.CreateNotificationChannel(channel);
    }
}
