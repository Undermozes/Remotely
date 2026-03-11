using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Enums;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Prompts the device owner for consent before allowing a remote control session,
/// consistent with the server's <c>EnforceAttendedAccess</c> setting.
/// </summary>
public class AndroidRemoteControlAccessService : IRemoteControlAccessService
{
    private readonly Context _context;
    private readonly ILogger<AndroidRemoteControlAccessService> _logger;
    private volatile bool _isPromptOpen;

    public AndroidRemoteControlAccessService(
        Context context,
        ILogger<AndroidRemoteControlAccessService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public bool IsPromptOpen => _isPromptOpen;

    public async Task<PromptForAccessResult> PromptForAccess(
        string requesterName,
        string organizationName)
    {
        if (_isPromptOpen)
        {
            return PromptForAccessResult.Denied;
        }

        _isPromptOpen = true;
        var tcs = new TaskCompletionSource<PromptForAccessResult>();

        try
        {
            var activity = MainActivity.Current;
            if (activity is null)
            {
                _logger.LogWarning("No active Activity found. Auto-accepting remote control request.");
                return PromptForAccessResult.Accepted;
            }

            activity.RunOnUiThread(() =>
            {
                try
                {
                    var dialog = new AlertDialog.Builder(activity)
                        .SetTitle("Remote Access Request")!
                        .SetMessage($"{requesterName} ({organizationName}) is requesting remote access to your device.")!
                        .SetPositiveButton("Allow", (s, e) =>
                        {
                            tcs.TrySetResult(PromptForAccessResult.Accepted);
                        })!
                        .SetNegativeButton("Deny", (s, e) =>
                        {
                            tcs.TrySetResult(PromptForAccessResult.Denied);
                        })!
                        .SetCancelable(false)!
                        .Create();

                    dialog?.Show();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while showing remote access dialog.");
                    tcs.TrySetResult(PromptForAccessResult.Error);
                }
            });

            // Time out after 30 seconds.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            cts.Token.Register(() => tcs.TrySetResult(PromptForAccessResult.TimedOut));

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during remote access prompt.");
            return PromptForAccessResult.Error;
        }
        finally
        {
            _isPromptOpen = false;
        }
    }
}
