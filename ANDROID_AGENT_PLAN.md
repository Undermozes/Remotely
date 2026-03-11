# Plan: Remote Desktop Agent for Android

## Overview

This document outlines the plan to add a native Android remote desktop agent to Remotely.
The agent will allow Android devices to be remotely controlled by operators through the
existing Remotely server, following the same architectural patterns used by the Windows
(`Desktop.Win`) and Linux (`Desktop.Linux`) desktop clients.

Two capabilities are in scope:

| Capability | Description |
|---|---|
| **Unattended Agent** | A background service (foreground service on Android) that stays connected to the Remotely server, reports device info, accepts scripts (ADB shell), and initiates screen-cast sessions. |
| **Remote Control (screen cast)** | Streams the device screen to the operator's browser and relays touch/keyboard input back to the device. |

---

## Architecture

The existing cross-platform desktop layer is structured as follows:

```
Desktop.Shared   – interfaces (IScreenCapturer, IKeyboardMouseInput, …)
Desktop.Core     – platform-agnostic logic (ScreenCaster, Viewer, CasterSocket, DtoMessageHandler)
Desktop.Win      – Windows-specific implementations
Desktop.Linux    – Linux-specific implementations
```

A new project **`Desktop.Android`** will be added alongside `Desktop.Win` and `Desktop.Linux`.
It will implement all `Desktop.Shared` abstractions using Android APIs, while reusing all
of `Desktop.Core` unchanged.

### High-level component diagram

```
┌──────────────────────────────────────────────────────┐
│                  Remotely Server                      │
│   AgentHub (SignalR)   RemoteControlHub (SignalR)     │
└──────┬───────────────────────────┬───────────────────┘
       │ SignalR                    │ SignalR
       ▼                            ▼
┌─────────────────┐        ┌──────────────────────────┐
│  Android Agent  │        │   Desktop.Android Client  │
│  (foreground    │──────▶ │   (screen cast session)   │
│   service)      │        │                           │
└─────────────────┘        │  Desktop.Core (reused)    │
                            │  Desktop.Android impls    │
                            └──────────────────────────┘
```

---

## New Project: `Desktop.Android`

### Technology choice

**.NET MAUI** (Multi-platform App UI) is the recommended implementation technology because:

* It is the strategic .NET successor to Xamarin.Android.
* It enables full code reuse of `Desktop.Core` and `Desktop.Shared` (both pure C# / .NET 8).
* The agent background service can be implemented as a MAUI project that targets
  `net8.0-android` without a visible UI (headless service mode).
* A `ForegroundService` wraps the SignalR hub connection for unattended operation.

### Project file location

```
Desktop.Android/
  Desktop.Android.csproj     (net8.0-android, MAUI)
  Platforms/
    Android/
      AndroidManifest.xml
      MainApplication.cs
      MainActivity.cs
  Services/
    AndroidScreenCapturer.cs
    AndroidKeyboardMouseInput.cs
    AndroidAudioCapturer.cs
    AndroidClipboardService.cs
    AndroidSessionIndicator.cs
    AndroidShutdownService.cs
    AndroidCursorIconWatcher.cs   (stub – no cursor on mobile)
    AndroidRemoteControlAccessService.cs
    AndroidFileTransferService.cs
    AndroidChatUiService.cs
    AndroidAppLauncher.cs
    AndroidDeviceInfoGenerator.cs
    AndroidUpdater.cs
  Agent/
    AndroidAgentForegroundService.cs
  Startup/
    MauiProgram.cs
```

---

## Interface Implementations

Each `Desktop.Shared` abstraction requires an Android-specific implementation.

### `IScreenCapturer` → `AndroidScreenCapturer`

Android 5.0+ (API 21) provides the **MediaProjection API** for screen capture without root.

Key steps:
1. Request `MediaProjection` via `MediaProjectionManager.createScreenCaptureIntent()` — this
   shows a one-time system confirmation dialog to the user.
2. Create a `VirtualDisplay` backed by an `ImageReader` (or `SurfaceTexture`).
3. Acquire frames from `ImageReader.acquireLatestImage()` and convert to `SKBitmap`
   via SkiaSharp (already a dependency in `Desktop.Core`).
4. Implement `GetFrameDiffArea()` using the same pixel-diff logic as the existing desktop
   implementations, or initially return the full screen bounds.
5. Store the `MediaProjection` token in a `ForegroundService` so capture survives when
   the app is in the background (required by Android 10+).

Android permissions required:
```xml
<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_MEDIA_PROJECTION" />
```

### `IKeyboardMouseInput` → `AndroidKeyboardMouseInput`

Input injection on Android requires either:

* **Accessibility Service** (recommended, no root required): Declare an
  `AccessibilityService` in `AndroidManifest.xml`; use `GestureDescription` to inject
  swipes, taps, and long-presses. Text input is handled via `AccessibilityNodeInfo` on
  the focused field, falling back to `InputManager.injectInputEvent` on rooted devices.
* **Root / ADB** (fallback): Use `Runtime.exec("input tap x y")` for rooted devices.

The `DtoMessageHandler` in `Desktop.Core` already translates incoming mouse/keyboard DTOs
into calls on `IKeyboardMouseInput`, so no changes to `Desktop.Core` are needed.

Mouse-event mapping:

| Desktop event | Android touch equivalent |
|---|---|
| Left click | Single tap |
| Right click | Long press |
| Click-and-drag | Long press + drag gesture |
| Mouse wheel | Scroll gesture |
| Key press | `AccessibilityNodeInfo.performAction(ACTION_SET_TEXT)` or virtual key injection |

Android permissions required:
```xml
<uses-permission android:name="android.permission.BIND_ACCESSIBILITY_SERVICE" />
```

### `IAudioCapturer` → `AndroidAudioCapturer`

Use `AudioRecord` with `MediaRecorder.AudioSource.REMOTE_SUBMIX` (API 21 / Android 5.0+) to capture
device audio output, then feed PCM samples to the existing audio pipeline in `Viewer.cs`.

Android permissions required:
```xml
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.CAPTURE_AUDIO_OUTPUT"
    tools:ignore="ProtectedPermissions" />
```

Note: `CAPTURE_AUDIO_OUTPUT` is a privileged permission and may require the app to be
installed as a system app or granted via ADB on non-rooted devices. This feature should be
optional and gracefully disabled when the permission is unavailable.

### `IClipboardService` → `AndroidClipboardService`

Use `ClipboardManager` (`Context.CLIPBOARD_SERVICE`) to read and write plain-text clipboard
data. Subscribe to `OnPrimaryClipChangedListener` for change notifications.

Android permissions required:
```xml
<!-- No special permission; direct API access. -->
```

### `ICursorIconWatcher` → `AndroidCursorIconWatcher` (stub)

Android has no hardware pointer cursor. This interface will be implemented as a no-op stub
that never raises the `CurrentCursorChanged` event.

### `ISessionIndicator` → `AndroidSessionIndicator`

Display a persistent `Notification` in a foreground service channel to inform the user
that a remote control session is active. The notification will include an action button
to end the session immediately.

Android permissions required:
```xml
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />  <!-- API 33+ -->
```

### `IShutdownService` → `AndroidShutdownService`

Stops the foreground service and disconnects from the hub. On Android, a full device
shutdown cannot be triggered by a third-party app.

### `IRemoteControlAccessService` → `AndroidRemoteControlAccessService`

Shows a confirmation dialog (via `AlertDialog`) to the device owner before allowing
a remote control session, consistent with the `EnforceAttendedAccess` server setting.

### `IFileTransferService` → `AndroidFileTransferService`

Uses Android `DownloadManager` or direct file I/O in the app's external files directory
(`Context.getExternalFilesDir()`).

Android permissions required:
```xml
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE"
    android:maxSdkVersion="28" />
```

---

## Android Agent Service

The agent component (equivalent to the existing `Agent` project on Windows/Linux) runs as
an Android **Foreground Service** (`AndroidAgentForegroundService`) so that it can stay
connected to the Remotely server even when the app is not in the foreground.

### Capabilities

| Capability | Implementation |
|---|---|
| Device info reporting | `AndroidDeviceInfoGenerator`: model, manufacturer, OS version, battery, IP, CPU utilization |
| Remote scripting | `ADB shell` via `Runtime.exec()` (rooted or via USB debugging) or Termux API |
| Screen cast | Delegates to `Desktop.Android` screen capture stack |
| Chat | Reuses existing `ChatClientService` from `Desktop.Core` |
| Wake-on-LAN | Not applicable for Android (no WoL emission) |
| App launch | `Context.startActivity()` with explicit component intent |
| Self-update | Download new APK from server update endpoint, invoke `PackageInstaller` |
| Uninstall | `Intent(Intent.ACTION_UNINSTALL_PACKAGE)` |

### SignalR hub connection

The existing `AgentHubConnection` in the `Agent` project depends on PowerShell SDK
(`Microsoft.PowerShell.SDK`) which is not available on Android. A new
`AndroidAgentHubConnection` will be created in `Desktop.Android/Agent/` that:

* Reuses `IAgentHubClient` interface from `Shared`.
* Replaces PowerShell scripting with shell command execution via `Runtime.exec()`.
* Otherwise inherits all SignalR connection lifecycle, heartbeat, and device reporting
  logic.

---

## Server-Side Changes

The Remotely server needs minimal changes because the Android agent uses the same
`AgentHub` and `RemoteControlHub` SignalR endpoints.

### Required additions

1. **`Platform` enum value** — add `Android` to `Shared/Enums/Platform.cs` alongside
   `Windows`, `Linux`, `MacOS`, and `Unknown`. The Android agent is functionally
   equivalent to the existing desktop agent (unattended management + screen cast) and is
   therefore classified as a platform variant, not a new device category. Using the
   existing `Platform` enum keeps the model consistent and avoids proliferating device
   classification enums.

2. **Device icon in Server UI** — add a smartphone icon to the device grid when
   `Platform == Android`.

3. **Install/update package endpoint** — add an endpoint analogous to the existing
   Windows/Linux installer download endpoints, serving the `.apk` file for Android
   agent distribution.

4. **Input-mapping awareness** (optional enhancement) — the viewer's remote control UI
   already supports mobile-style tap/long-press mappings (documented in README). No code
   change is required; the existing web viewer will work as-is.

---

## Dependency on Existing Projects

```
Desktop.Android.csproj
  ├── ProjectReference → Desktop.Core.csproj       (reused entirely)
  ├── ProjectReference → Desktop.Shared.csproj     (interfaces to implement)
  └── ProjectReference → Shared.csproj             (DTOs, models, enums)
```

No changes to `Desktop.Core` or `Desktop.Shared` are anticipated. If any abstraction
needs a minor addition to support Android (e.g., a touch-specific input method), it will
be added as an optional interface member with a default no-op implementation.

---

## Permissions Summary

```xml
<!-- AndroidManifest.xml -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_MEDIA_PROJECTION" />
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.BIND_ACCESSIBILITY_SERVICE" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_WIFI_STATE" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE"
    android:maxSdkVersion="28" />
<!-- Privileged – gracefully disabled if unavailable -->
<uses-permission android:name="android.permission.CAPTURE_AUDIO_OUTPUT"
    tools:ignore="ProtectedPermissions" />
```

---

## Implementation Phases

### Phase 1 – Unattended Agent (no screen cast)

Goal: Android device appears in the Remotely device list and accepts scripted commands.

- [ ] Create `Desktop.Android` MAUI project targeting `net8.0-android`.
- [ ] Implement `AndroidAgentForegroundService` with SignalR connection to `AgentHub`.
- [ ] Implement `AndroidDeviceInfoGenerator` (model, OS, battery, IP, CPU).
- [ ] Implement basic shell-command execution via `Runtime.exec()`.
- [ ] Add `DeviceType.Android` to `Shared/Enums/DeviceType.cs`.
- [ ] Surface Android device icon in the Server device grid.
- [ ] Add APK download endpoint to the Server.
- [ ] Write unit tests for `AndroidDeviceInfoGenerator`.

### Phase 2 – Screen Cast (attended)

Goal: Operator can view the Android screen in real time via the browser.

- [ ] Implement `AndroidScreenCapturer` using `MediaProjection` + `VirtualDisplay`.
- [ ] Wire `AndroidScreenCapturer` into `Desktop.Core.ScreenCaster`.
- [ ] Implement `AndroidSessionIndicator` (foreground notification).
- [ ] Implement stub `AndroidCursorIconWatcher`.
- [ ] Implement `AndroidShutdownService`.
- [ ] Show user-consent dialog before starting screen cast.
- [ ] Integration test: full screen-cast session from Android emulator to local server.

### Phase 3 – Remote Input (full remote control)

Goal: Operator can interact with the Android device (touch, text input).

- [ ] Implement `AndroidKeyboardMouseInput` via `AccessibilityService` + `GestureDescription`.
- [ ] Map incoming `MouseMoveDto`, `MouseButtonDto`, `KeyboardInputDto` from
  `DtoMessageHandler` to Android gestures.
- [ ] Implement `AndroidClipboardService`.
- [ ] Implement `AndroidRemoteControlAccessService` (consent dialog).

### Phase 4 – File Transfer and Audio

Goal: Feature parity with Windows/Linux desktop clients.

- [ ] Implement `AndroidFileTransferService`.
- [ ] Implement `AndroidAudioCapturer` (gracefully disabled without privileged permission).
- [ ] Implement `AndroidChatUiService`.
- [ ] Implement `AndroidUpdater` (APK download + `PackageInstaller` session).

---

## Technical Challenges

| Challenge | Mitigation |
|---|---|
| MediaProjection requires user consent on every boot | Persist grant token in encrypted preferences; re-prompt if token is invalid |
| Background service killed by battery optimisation (Doze, App Standby) | Request exemption via `ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`; document for end users |
| Accessibility service must be manually enabled by the user | Guide user through Settings > Accessibility on first launch |
| Input injection restricted to root on many OEMs | Clearly document root-only features; provide graceful degradation |
| PowerShell not available on Android | Replace with `Runtime.exec()` shell commands for scripting |
| Screen recording blocked on some vendor ROMs | Document known incompatibilities; test on emulator and major OEM images |
| APK distribution / auto-update outside Play Store | Serve APK directly from Remotely server; require "Install from unknown sources" |
| `.NET MAUI` / Android toolchain adds build complexity | Add dedicated `build-android.yml` GitHub Actions workflow |
| SkiaSharp `ImageReader` ↔ `SKBitmap` conversion performance | Benchmark on low-end hardware; consider hardware JPEG encoder via `MediaCodec` |

---

## Build & CI

A new GitHub Actions workflow `build-android.yml` will be added:

```yaml
name: Build Android Agent
on:
  push:
    branches: [master]
  workflow_dispatch:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Install MAUI workload
        run: dotnet workload install maui-android
      - name: Build Desktop.Android
        run: dotnet build Desktop.Android/Desktop.Android.csproj -c Release
      - name: Run tests
        run: dotnet test Tests/Desktop.Android.Tests/Desktop.Android.Tests.csproj
```

---

## References

* [Android MediaProjection API](https://developer.android.com/reference/android/media/projection/MediaProjection)
* [Android AccessibilityService](https://developer.android.com/reference/android/accessibilityservice/AccessibilityService)
* [GestureDescription (touch injection)](https://developer.android.com/reference/android/accessibilityservice/GestureDescription)
* [.NET MAUI Android platform docs](https://learn.microsoft.com/en-us/dotnet/maui/android/)
* [Foreground services on Android](https://developer.android.com/guide/components/foreground-services)
* [SkiaSharp on MAUI](https://github.com/mono/SkiaSharp)
* Existing Windows implementation: `Desktop.Win/`
* Existing Linux implementation: `Desktop.Linux/`
* Shared abstractions: `Desktop.Shared/Abstractions/`
* Core screen-cast logic: `Desktop.Core/Services/ScreenCaster.cs`, `Viewer.cs`
