using Android.Content;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Enums;
using Remotely.Desktop.Shared.Services;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Coordinates the Android remote control client startup sequence.
/// </summary>
internal class AndroidAppStartup : IAppStartup
{
    private readonly IAppState _appState;
    private readonly IKeyboardMouseInput _inputService;
    private readonly IDesktopHubConnection _desktopHub;
    private readonly IClipboardService _clipboardService;
    private readonly IChatHostService _chatHostService;
    private readonly ICursorIconWatcher _cursorIconWatcher;
    private readonly IIdleTimer _idleTimer;
    private readonly IShutdownService _shutdownService;
    private readonly IBrandingProvider _brandingProvider;
    private readonly ILogger<AndroidAppStartup> _logger;

    public AndroidAppStartup(
        IAppState appState,
        IKeyboardMouseInput inputService,
        IDesktopHubConnection desktopHub,
        IClipboardService clipboardService,
        IChatHostService chatHostService,
        ICursorIconWatcher cursorIconWatcher,
        IIdleTimer idleTimer,
        IShutdownService shutdownService,
        IBrandingProvider brandingProvider,
        ILogger<AndroidAppStartup> logger)
    {
        _appState = appState;
        _inputService = inputService;
        _desktopHub = desktopHub;
        _clipboardService = clipboardService;
        _chatHostService = chatHostService;
        _cursorIconWatcher = cursorIconWatcher;
        _idleTimer = idleTimer;
        _shutdownService = shutdownService;
        _brandingProvider = brandingProvider;
        _logger = logger;
    }

    public async Task Run()
    {
        await _brandingProvider.Initialize();

        if (_appState.Mode is AppMode.Unattended or AppMode.Attended)
        {
            _clipboardService.BeginWatching();
            _inputService.Init();
            // Android has no hardware cursor; no event subscription needed.
        }

        switch (_appState.Mode)
        {
            case AppMode.Unattended:
                await StartScreenCasting();
                break;

            case AppMode.Attended:
                // Attended mode for Android: wait for incoming session requests
                // via SignalR without broadcasting a session ID.
                await StartScreenCasting();
                break;

            case AppMode.Chat:
                await _chatHostService
                    .StartChat(_appState.PipeName, _appState.OrganizationName)
                    .ConfigureAwait(false);
                break;

            default:
                break;
        }
    }

    private async Task StartScreenCasting()
    {
        if (!await _desktopHub.Connect(TimeSpan.FromSeconds(30), CancellationToken.None))
        {
            _logger.LogError("Failed to connect to Remotely server.");
            await _shutdownService.Shutdown();
            return;
        }

        var result = await _desktopHub.SendUnattendedSessionInfo(
            _appState.SessionId,
            _appState.AccessKey,
            global::Android.OS.Build.Model ?? "Android Device",
            _appState.RequesterName,
            _appState.OrganizationName);

        if (!result.IsSuccess)
        {
            _logger.LogError(result.Exception, "Failed to send session info to server.");
            await _shutdownService.Shutdown();
            return;
        }

        try
        {
            if (_appState.ArgDict.ContainsKey("relaunch"))
            {
                _logger.LogInformation("Resuming after relaunch.");
                await _desktopHub.NotifyViewersRelaunchedScreenCasterReady(_appState.RelaunchViewers);
            }
            else
            {
                await _desktopHub.NotifyRequesterUnattendedReady();
            }
        }
        finally
        {
            _idleTimer.Start();
        }
    }
}
