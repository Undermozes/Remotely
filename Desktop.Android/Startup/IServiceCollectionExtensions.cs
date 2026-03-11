using Android.Content;
using Microsoft.Extensions.DependencyInjection;
using Remotely.Desktop.Android.Services;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Startup;

namespace Remotely.Desktop.Android.Startup;

/// <summary>
/// Extension methods for registering Android remote control services.
/// </summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Android-specific and cross-platform remote control services.
    /// </summary>
    public static void AddRemoteControlAndroid(
        this IServiceCollection services,
        Context context)
    {
        // Register the Android context as a singleton for services that need it.
        services.AddSingleton(context);

        // Register cross-platform core services.
        services.AddRemoteControlXplat();

        // Android-specific implementations of Desktop.Shared abstractions.
        services.AddSingleton<IAndroidScreenCapturer, AndroidScreenCapturer>();
        services.AddTransient<IScreenCapturer>(sp => sp.GetRequiredService<IAndroidScreenCapturer>());

        services.AddSingleton<IKeyboardMouseInput, AndroidKeyboardMouseInput>();
        services.AddSingleton<IAudioCapturer, AndroidAudioCapturer>();
        services.AddSingleton<IClipboardService, AndroidClipboardService>();
        services.AddSingleton<ICursorIconWatcher, AndroidCursorIconWatcher>();
        services.AddSingleton<ISessionIndicator, AndroidSessionIndicator>();
        services.AddSingleton<IShutdownService, AndroidShutdownService>();
        services.AddSingleton<IRemoteControlAccessService, AndroidRemoteControlAccessService>();
        services.AddScoped<IFileTransferService, AndroidFileTransferService>();
        services.AddSingleton<IChatUiService, AndroidChatUiService>();
        services.AddSingleton<IAppStartup, AndroidAppStartup>();
    }
}
