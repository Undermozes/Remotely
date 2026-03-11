using Android.AccessibilityServices;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Enums;
using Remotely.Desktop.Shared.Services;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Injects touch and keyboard input on Android using the AccessibilityService API.
/// The user must enable "Remotely Input Control" in Android Settings → Accessibility
/// before this service can inject gestures.
/// </summary>
public class AndroidKeyboardMouseInput : IKeyboardMouseInput
{
    private readonly ILogger<AndroidKeyboardMouseInput> _logger;

    public AndroidKeyboardMouseInput(ILogger<AndroidKeyboardMouseInput> logger)
    {
        _logger = logger;
    }

    public void Init()
    {
        // Nothing to initialise here.  Gesture injection is performed directly
        // through RemotelyAccessibilityService.Instance when available.
    }

    public void SendKeyDown(string key)
    {
        try
        {
            var keyCode = ConvertJavaScriptKeyToKeyCode(key);
            if (keyCode == Keycode.Unknown)
            {
                _logger.LogWarning("Unmapped key: {Key}", key);
                return;
            }

            var service = RemotelyAccessibilityService.Instance;
            if (service is null)
            {
                _logger.LogWarning("AccessibilityService not available. Ensure the service is enabled in Settings.");
                return;
            }

            service.InjectKeyEvent(new KeyEvent(KeyEventActions.Down, keyCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending key down: {Key}", key);
        }
    }

    public void SendKeyUp(string key)
    {
        try
        {
            var keyCode = ConvertJavaScriptKeyToKeyCode(key);
            if (keyCode == Keycode.Unknown)
            {
                _logger.LogWarning("Unmapped key: {Key}", key);
                return;
            }

            var service = RemotelyAccessibilityService.Instance;
            if (service is null)
            {
                _logger.LogWarning("AccessibilityService not available.");
                return;
            }

            service.InjectKeyEvent(new KeyEvent(KeyEventActions.Up, keyCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending key up: {Key}", key);
        }
    }

    public void SendMouseButtonAction(
        int button,
        ButtonAction buttonAction,
        double percentX,
        double percentY,
        IViewer viewer)
    {
        try
        {
            var service = RemotelyAccessibilityService.Instance;
            if (service is null)
            {
                _logger.LogWarning("AccessibilityService not available.");
                return;
            }

            var bounds = viewer.Capturer.CurrentScreenBounds;
            var x = (float)(bounds.Width * percentX);
            var y = (float)(bounds.Height * percentY);

            switch (button)
            {
                case 0: // Left (primary)
                    if (buttonAction == ButtonAction.Down)
                    {
                        service.InjectTapDown(x, y);
                    }
                    else
                    {
                        service.InjectTapUp(x, y);
                    }
                    break;

                case 2: // Right – simulate as long-press on Android
                    if (buttonAction == ButtonAction.Down)
                    {
                        service.InjectLongPress(x, y);
                    }
                    break;

                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending mouse button action.");
        }
    }

    public void SendMouseMove(double percentX, double percentY, IViewer viewer)
    {
        try
        {
            var service = RemotelyAccessibilityService.Instance;
            if (service is null)
            {
                return;
            }

            var bounds = viewer.Capturer.CurrentScreenBounds;
            var x = (float)(bounds.Width * percentX);
            var y = (float)(bounds.Height * percentY);
            service.InjectSwipe(x, y, x, y, durationMs: 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending mouse move.");
        }
    }

    public void SendMouseWheel(int deltaY)
    {
        try
        {
            var service = RemotelyAccessibilityService.Instance;
            if (service is null)
            {
                return;
            }

            // Simulate a vertical swipe to represent scrolling.
            var centerX = service.ScreenWidth / 2f;
            var centerY = service.ScreenHeight / 2f;
            var distance = deltaY > 0 ? -200f : 200f;
            service.InjectSwipe(centerX, centerY, centerX, centerY + distance, durationMs: 200);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending mouse wheel.");
        }
    }

    public void SendText(string transferText)
    {
        try
        {
            var service = RemotelyAccessibilityService.Instance;
            if (service is null)
            {
                _logger.LogWarning("AccessibilityService not available for text input.");
                return;
            }

            service.InjectText(transferText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending text.");
        }
    }

    public void SetKeyStatesUp()
    {
        // Not applicable on Android.
    }

    public void ToggleBlockInput(bool toggleOn)
    {
        // Blocking input programmatically is not supported without root on Android.
        _logger.LogWarning("ToggleBlockInput is not supported on Android without root.");
    }

    private static Keycode ConvertJavaScriptKeyToKeyCode(string key)
    {
        return key switch
        {
            " " => Keycode.Space,
            "ArrowDown" or "Down" => Keycode.DpadDown,
            "ArrowUp" or "Up" => Keycode.DpadUp,
            "ArrowLeft" or "Left" => Keycode.DpadLeft,
            "ArrowRight" or "Right" => Keycode.DpadRight,
            "Enter" => Keycode.Enter,
            "Esc" or "Escape" => Keycode.Escape,
            "Backspace" => Keycode.Del,
            "Tab" => Keycode.Tab,
            "Delete" => Keycode.ForwardDel,
            "Home" => Keycode.Home,
            "End" => Keycode.MoveEnd,
            "PageUp" => Keycode.PageUp,
            "PageDown" => Keycode.PageDown,
            "Control" => Keycode.CtrlLeft,
            "Shift" => Keycode.ShiftLeft,
            "Alt" => Keycode.AltLeft,
            "Meta" => Keycode.MetaLeft,
            "CapsLock" => Keycode.CapsLock,
            "F1" => Keycode.F1,
            "F2" => Keycode.F2,
            "F3" => Keycode.F3,
            "F4" => Keycode.F4,
            "F5" => Keycode.F5,
            "F6" => Keycode.F6,
            "F7" => Keycode.F7,
            "F8" => Keycode.F8,
            "F9" => Keycode.F9,
            "F10" => Keycode.F10,
            "F11" => Keycode.F11,
            "F12" => Keycode.F12,
            _ when key.Length == 1 => MapCharToKeycode(key[0]),
            _ => Keycode.Unknown
        };
    }

    private static Keycode MapCharToKeycode(char c)
    {
        return c switch
        {
            'a' or 'A' => Keycode.A,
            'b' or 'B' => Keycode.B,
            'c' or 'C' => Keycode.C,
            'd' or 'D' => Keycode.D,
            'e' or 'E' => Keycode.E,
            'f' or 'F' => Keycode.F,
            'g' or 'G' => Keycode.G,
            'h' or 'H' => Keycode.H,
            'i' or 'I' => Keycode.I,
            'j' or 'J' => Keycode.J,
            'k' or 'K' => Keycode.K,
            'l' or 'L' => Keycode.L,
            'm' or 'M' => Keycode.M,
            'n' or 'N' => Keycode.N,
            'o' or 'O' => Keycode.O,
            'p' or 'P' => Keycode.P,
            'q' or 'Q' => Keycode.Q,
            'r' or 'R' => Keycode.R,
            's' or 'S' => Keycode.S,
            't' or 'T' => Keycode.T,
            'u' or 'U' => Keycode.U,
            'v' or 'V' => Keycode.V,
            'w' or 'W' => Keycode.W,
            'x' or 'X' => Keycode.X,
            'y' or 'Y' => Keycode.Y,
            'z' or 'Z' => Keycode.Z,
            '0' => Keycode.Num0,
            '1' => Keycode.Num1,
            '2' => Keycode.Num2,
            '3' => Keycode.Num3,
            '4' => Keycode.Num4,
            '5' => Keycode.Num5,
            '6' => Keycode.Num6,
            '7' => Keycode.Num7,
            '8' => Keycode.Num8,
            '9' => Keycode.Num9,
            '.' => Keycode.Period,
            ',' => Keycode.Comma,
            '-' => Keycode.Minus,
            '+' => Keycode.Plus,
            '=' => Keycode.Equals,
            _ => Keycode.Unknown
        };
    }
}
