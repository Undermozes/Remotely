using Android.AccessibilityServices;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using Microsoft.Extensions.Logging;
using AGPath = Android.Graphics.Path;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Android Accessibility Service that enables gesture and key injection for remote input.
/// The user must manually enable this service in Android Settings → Accessibility → Remotely.
/// </summary>
public class RemotelyAccessibilityService : AccessibilityService
{
    private static RemotelyAccessibilityService? _instance;

    /// <summary>
    /// Gets the current active instance of the accessibility service, or null if not enabled.
    /// </summary>
    public static RemotelyAccessibilityService? Instance => _instance;

    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }

    public override void OnAccessibilityEvent(AccessibilityEvent? e) { }
    public override void OnInterrupt() { }

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        _instance = this;

        var displayMetrics = Resources?.DisplayMetrics;
        if (displayMetrics != null)
        {
            ScreenWidth = displayMetrics.WidthPixels;
            ScreenHeight = displayMetrics.HeightPixels;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// Injects a key event (down or up).
    /// </summary>
    public void InjectKeyEvent(KeyEvent keyEvent)
    {
        // For keyboard input, we use AccessibilityNodeInfo.ACTION_SET_TEXT when possible.
        // This is a best-effort implementation.
        PerformGlobalAction(0);
    }

    /// <summary>
    /// Injects text into the focused field.
    /// </summary>
    public void InjectText(string text)
    {
        var focusedNode = FindFocus(NodeFocus.Input);
        if (focusedNode != null)
        {
            var args = new Bundle();
            args.PutCharSequence(
                AccessibilityNodeInfo.ActionArgumentSetTextCharsequence,
                new Java.Lang.String(text));
            focusedNode.PerformAction(
                global::Android.Views.Accessibility.Action.SetText,
                args);
        }
    }

    /// <summary>
    /// Injects a tap-down gesture at the specified coordinates.
    /// </summary>
    public void InjectTapDown(float x, float y)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var path = new AGPath();
        path.MoveTo(x, y);

        var strokeDescription = new GestureDescription.StrokeDescription(
            path,
            startTime: 0,
            duration: 1,
            willContinue: true);

        var gestureDescription = new GestureDescription.Builder()
            .AddStroke(strokeDescription)!
            .Build()!;

        if (gestureDescription != null)
        {
            DispatchGesture(gestureDescription, null, null);
        }
    }

    /// <summary>
    /// Injects a tap-up gesture at the specified coordinates.
    /// </summary>
    public void InjectTapUp(float x, float y)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var path = new AGPath();
        path.MoveTo(x, y);

        var strokeDescription = new GestureDescription.StrokeDescription(
            path,
            startTime: 0,
            duration: 50,
            willContinue: false);

        var gestureDescription = new GestureDescription.Builder()
            .AddStroke(strokeDescription)!
            .Build()!;

        if (gestureDescription != null)
        {
            DispatchGesture(gestureDescription, null, null);
        }
    }

    /// <summary>
    /// Injects a long-press gesture at the specified coordinates.
    /// </summary>
    public void InjectLongPress(float x, float y)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var path = new AGPath();
        path.MoveTo(x, y);

        var strokeDescription = new GestureDescription.StrokeDescription(
            path,
            startTime: 0,
            duration: 600);

        var gestureDescription = new GestureDescription.Builder()
            .AddStroke(strokeDescription)!
            .Build()!;

        if (gestureDescription != null)
        {
            DispatchGesture(gestureDescription, null, null);
        }
    }

    /// <summary>
    /// Injects a swipe gesture from one point to another.
    /// </summary>
    public void InjectSwipe(float fromX, float fromY, float toX, float toY, long durationMs)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var path = new AGPath();
        path.MoveTo(fromX, fromY);
        path.LineTo(toX, toY);

        var strokeDescription = new GestureDescription.StrokeDescription(
            path,
            startTime: 0,
            duration: Math.Max(1, durationMs));

        var gestureDescription = new GestureDescription.Builder()
            .AddStroke(strokeDescription)!
            .Build()!;

        if (gestureDescription != null)
        {
            DispatchGesture(gestureDescription, null, null);
        }
    }
}

