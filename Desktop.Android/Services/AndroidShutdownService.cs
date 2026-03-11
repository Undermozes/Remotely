using Android.Content;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Services;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Shuts down the Remotely Android remote control session by disconnecting all
/// viewers and stopping the foreground service.
/// </summary>
public class AndroidShutdownService : IShutdownService
{
    private readonly Context _context;
    private readonly IDesktopHubConnection _hubConnection;
    private readonly IAppState _appState;
    private readonly ILogger<AndroidShutdownService> _logger;

    public AndroidShutdownService(
        Context context,
        IDesktopHubConnection hubConnection,
        IAppState appState,
        ILogger<AndroidShutdownService> logger)
    {
        _context = context;
        _hubConnection = hubConnection;
        _appState = appState;
        _logger = logger;
    }

    public async Task Shutdown()
    {
        _logger.LogInformation("Shutting down Remotely Android agent.");

        await TryDisconnectViewers();

        // Stop the foreground service.
        var serviceIntent = new Intent(_context, typeof(AgentForegroundService));
        _context.StopService(serviceIntent);
    }

    private async Task TryDisconnectViewers()
    {
        try
        {
            if (_hubConnection.IsConnected && _appState.Viewers.Any())
            {
                await _hubConnection.DisconnectAllViewers();
                await _hubConnection.Disconnect();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disconnecting viewers during shutdown.");
        }
    }
}
