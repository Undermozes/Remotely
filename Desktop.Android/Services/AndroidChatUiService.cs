using Android.App;
using Android.Content;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Provides a simple chat UI on Android by showing an alert dialog for incoming
/// messages and posting outgoing messages directly to the writer stream.
/// A future iteration can implement a richer notification-based chat UI.
/// </summary>
public class AndroidChatUiService : IChatUiService
{
    private readonly Context _context;
    private readonly ILogger<AndroidChatUiService> _logger;

    private StreamWriter? _writer;
    private bool _windowOpen;

    public event EventHandler? ChatWindowClosed;

    public AndroidChatUiService(
        Context context,
        ILogger<AndroidChatUiService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ReceiveChat(ChatMessage chatMessage)
    {
        try
        {
            var activity = MainActivity.Current;
            if (activity is null)
            {
                _logger.LogWarning("No active Activity. Cannot display chat message.");
                return;
            }

            activity.RunOnUiThread(() =>
            {
                new AlertDialog.Builder(activity)
                    .SetTitle($"Message from {chatMessage.SenderName}")!
                    .SetMessage(chatMessage.Message)!
                    .SetPositiveButton("OK", (IDialogInterfaceOnClickListener?)null)!
                    .Show();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while receiving chat message.");
        }
        await Task.CompletedTask;
    }

    public void ShowChatWindow(string organizationName, StreamWriter writer)
    {
        _writer = writer;
        _windowOpen = true;

        var activity = MainActivity.Current;
        if (activity is null)
        {
            _logger.LogWarning("No active Activity. Cannot show chat window.");
            return;
        }

        // On Android, "show chat window" means a minimal prompt dialog for the device owner.
        activity.RunOnUiThread(() =>
        {
            new AlertDialog.Builder(activity)
                .SetTitle($"Remote Chat – {organizationName}")!
                .SetMessage("A remote operator has started a chat session.")!
                .SetPositiveButton("OK", (IDialogInterfaceOnClickListener?)null)!
                .Show();
        });
    }
}
