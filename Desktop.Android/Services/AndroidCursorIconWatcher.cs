using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;
using System.Drawing;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Cursor icon watcher stub for Android.
/// Android has no hardware pointer cursor, so this is a permanent no-op.
/// </summary>
public class AndroidCursorIconWatcher : ICursorIconWatcher
{
#pragma warning disable CS0067
    // Obsolete as noted in the interface; never raised on Android.
    [Obsolete("This should be replaced with a message published by IMessenger.")]
    public event EventHandler<CursorInfo>? OnChange;
#pragma warning restore CS0067

    public CursorInfo GetCurrentCursor() =>
        new(Array.Empty<byte>(), Point.Empty, "default");
}
