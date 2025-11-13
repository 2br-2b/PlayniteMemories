# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SharpMemories is a Playnite plugin that automatically captures screenshots of games at configurable intervals. The plugin is written in C# using .NET Framework 4.6.2 and integrates with the Playnite SDK v6.13.0.

## Build Commands

### Development Build
```bash
# Build debug version
msbuild SharpMemories.sln /p:Configuration=Debug

# Or using dotnet (if available)
dotnet build SharpMemories.sln --configuration Debug
```

### Production Build
```bash
# Build release version
msbuild SharpMemories.sln /p:Configuration=Release

# Or using dotnet (if available)
dotnet build SharpMemories.sln --configuration Release
```

### Using Visual Studio
```bash
# Open solution in Visual Studio
start SharpMemories.sln
```

## Architecture

### Core Components

1. **SharpMemories.cs** - Main plugin class that extends `GenericPlugin`
   - Handles Playnite lifecycle events (game start/stop, application start/stop)
   - Delegates to three manager classes: screenshot capture, folder monitoring, and keyboard hook
   - Coordinates lifecycle between multiple managers
   - Implements per-library hotkey filtering (Steam defaults to disabled)

2. **ScreenshotCaptureManager.cs** - Screenshot capture lifecycle management
   - Manages async capture loop with configurable intervals
   - Implements thread-safe capture control with cancellation tokens
   - Handles process-based window capture with fallback to full screen
   - Creates game-specific subfolders under output folder
   - Supports on-demand capture via hotkey with sound feedback

3. **FolderMonitorManager.cs** - External screenshot folder monitoring
   - Uses FileSystemWatcher to detect new files in a monitored folder
   - Automatically moves detected screenshots to game-specific folders
   - Waits for file accessibility before moving (handles locked files with 10 second timeout)
   - Useful for games with built-in screenshot features (e.g., Steam games)

4. **KeyboardHookManager.cs** - Global hotkey support
   - Implements low-level keyboard hooks using Win32 API
   - Registers configurable hotkey combinations (key + Ctrl/Alt/Shift modifiers)
   - Triggers on-demand screenshot capture when hotkey is pressed
   - Per-library enable/disable support
   - Properly disposes hooks on game stop and application exit

5. **ScreenCapture.cs** - Low-level screenshot functionality
   - Uses Win32 API (user32.dll, gdi32.dll) for capturing windows and screen
   - Provides `CaptureWindow()` and `CaptureScreen()` methods
   - Handles memory management for native resources (DC handles, bitmaps)

6. **FileHelpers.cs** - Filename utilities
   - `MakeSafeFilename()` - Strips invalid filesystem characters from game names

7. **SteamHelpers.cs** - Steam integration utilities
   - `GetSteamScreenshotFolder()` - Auto-detects Steam screenshot folder for most recent user
   - `GetAllSteamScreenshotFolders()` - Returns all Steam user screenshot folders
   - Searches common Steam installation paths
   - Used to set default monitor folder on first run

8. **SharpMemoriesSettings.cs** - Settings management
   - `SharpMemoriesSettings` - Data model for plugin configuration with per-library hotkey settings
   - `SharpMemoriesSettingsViewModel` - MVVM wrapper implementing `ISettings`
   - `LibraryPluginInfo` - Helper class for UI binding to library plugin settings
   - Handles serialization/deserialization of user preferences
   - Manages dynamic per-library hotkey enable flags (keyed by library plugin GUID)

9. **SharpMemoriesSettingsView.xaml/.cs** - WPF settings UI
   - User interface for configuring plugin options
   - Data-bound to settings view model
   - Supports hotkey recording UI

### Plugin Lifecycle

- **OnGameStarted**: Starts screenshot capture loop, folder monitoring, and registers hotkey (if enabled) for the launched game
- **OnGameStopped**: Terminates capture loop, stops folder monitoring, and unregisters hotkey when game closes
- **OnApplicationStopped**: Cleanup when Playnite exits (disposes keyboard hook)

### Triple Screenshot System

The plugin supports three concurrent screenshot mechanisms:

**Active Capture (ScreenshotCaptureManager - Timer-based)**:
1. Starts async task when game launches
2. Waits for configured interval, then captures screenshot
3. Attempts to capture specific game window by process ID first
4. Falls back to full screen capture if window capture fails
5. Saves as PNG with format: `{GameName}_{yyyyMMdd_HHmmss}.png`
6. Organizes screenshots into game-specific subfolders

**Passive Monitoring (FolderMonitorManager)**:
1. Watches a user-specified folder for new files (e.g., game's built-in screenshot folder)
2. Detects file creation events via FileSystemWatcher
3. Waits for file to become accessible (up to 10 seconds, checking every 100ms)
4. Moves file to the same game-specific subfolder as active captures
5. Useful for consolidating screenshots from games with native screenshot features

**On-Demand Capture (KeyboardHookManager)**:
1. Registers a global keyboard hook with configurable hotkey combination
2. Triggers immediate screenshot capture when hotkey is pressed during gameplay
3. Plays system sound (Asterisk) as feedback
4. Can be enabled/disabled per library (Steam defaults to disabled to avoid conflicts)
5. Uses same output folder structure as active capture

### Key Configuration

- **Enabled**: Toggle automatic screenshot capture
- **IntervalMinutes**: Time between screenshots (default: 15 minutes)
- **OutputFolder**: Directory for saved screenshots (defaults to `%UserProfile%\Pictures\Playnite`)
- **MonitorFolder**: Optional folder to monitor for external screenshots (defaults to Steam screenshot folder if detected)
- **EnableMonitoring**: Toggle folder monitoring feature
- **EnableHotkey**: Toggle hotkey feature
- **HotkeyKey**: Key for hotkey (default: F12)
- **HotkeyCtrl/HotkeyAlt/HotkeyShift**: Modifier keys for hotkey combination
- **HotkeyEnabledByLibrary**: Dictionary mapping library plugin GUIDs to hotkey enable flags (Steam defaults to false)

### Important Implementation Details

**Per-Library Hotkey Filtering**:
- Steam library (CB91DFC9-B977-43BF-8E70-55F46E410FAB) defaults to hotkey disabled to avoid conflicts with Steam's built-in screenshot feature
- Other libraries default to hotkey enabled
- User can override per-library settings in UI
- Filtering happens in `ShouldEnableHotkeyForGame()` in SharpMemories.cs:35

**Thread Safety**:
- Screenshot capture uses `captureLock` object to protect concurrent access to capture state
- FileSystemWatcher events run on background thread with proper error handling

**Resource Management**:
- Native GDI handles properly released in finally blocks
- CancellationTokenSource disposed after use
- KeyboardHookManager implements IDisposable for proper cleanup
- FileSystemWatcher disposed when monitoring stops

## File Structure

```
SharpMemories/
├── SharpMemories.cs              # Main plugin implementation
├── ScreenshotCaptureManager.cs   # Screenshot capture lifecycle
├── FolderMonitorManager.cs       # External folder monitoring
├── KeyboardHookManager.cs        # Global hotkey support
├── ScreenCapture.cs              # Native Win32 screenshot utilities
├── FileHelpers.cs                # Filename sanitization utilities
├── SteamHelpers.cs               # Steam integration utilities
├── SharpMemoriesSettings.cs      # Settings data model and view model
├── SharpMemoriesSettingsView.xaml # Settings UI layout
├── SharpMemoriesSettingsView.xaml.cs # Settings UI code-behind
├── extension.yaml               # Plugin manifest
├── SharpMemories.csproj         # Project file
├── SharpMemories.sln           # Solution file
├── packages.config             # NuGet package references
├── App.xaml                    # WPF application resources
├── icon.png                    # Plugin icon
└── Localization/
    └── en_US.xaml             # English localization resources
```

## Dependencies

- **Playnite.SDK 6.13.0** - Core plugin API
- **.NET Framework 4.6.2** - Runtime framework
- **WPF** - UI framework (PresentationCore, PresentationFramework)
- **System.Drawing** - Image manipulation
- **System.Windows.Forms** - Screen bounds detection

