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
   - Delegates to manager classes for screenshot capture and folder monitoring
   - Coordinates lifecycle between multiple managers

2. **ScreenshotCaptureManager.cs** - Screenshot capture lifecycle management
   - Manages async capture loop with configurable intervals
   - Implements thread-safe capture control with cancellation tokens
   - Handles process-based window capture with fallback to full screen
   - Creates game-specific subfolders under output folder

3. **FolderMonitorManager.cs** - External screenshot folder monitoring
   - Uses FileSystemWatcher to detect new files in a monitored folder
   - Automatically moves detected screenshots to game-specific folders
   - Waits for file accessibility before moving (handles locked files)
   - Useful for games with built-in screenshot features

4. **ScreenCapture.cs** - Low-level screenshot functionality
   - Uses Win32 API (user32.dll, gdi32.dll) for capturing windows and screen
   - Provides `CaptureWindow()` and `CaptureScreen()` methods
   - Handles memory management for native resources (DC handles, bitmaps)

5. **FileHelpers.cs** - Filename utilities
   - `MakeSafeFilename()` - Strips invalid filesystem characters from game names

6. **SharpMemoriesSettings.cs** - Settings management
   - `SharpMemoriesSettings` - Data model for plugin configuration
   - `SharpMemoriesSettingsViewModel` - MVVM wrapper implementing `ISettings`
   - Handles serialization/deserialization of user preferences

7. **SharpMemoriesSettingsView.xaml/.cs** - WPF settings UI
   - User interface for configuring plugin options
   - Data-bound to settings view model

### Plugin Lifecycle

- **OnGameStarted**: Starts screenshot capture loop and folder monitoring for the launched game
- **OnGameStopped**: Terminates capture loop and stops folder monitoring when game closes
- **OnApplicationStopped**: Cleanup when Playnite exits

### Dual Screenshot System

The plugin supports two concurrent screenshot mechanisms:

**Active Capture (ScreenshotCaptureManager)**:
1. Starts async task when game launches
2. Waits for configured interval, then captures screenshot
3. Attempts to capture specific game window by process ID first
4. Falls back to full screen capture if window capture fails
5. Saves as PNG with format: `{GameName}_{yyyyMMdd_HHmmss}.png`
6. Organizes screenshots into game-specific subfolders

**Passive Monitoring (FolderMonitorManager)**:
1. Watches a user-specified folder for new files (e.g., game's built-in screenshot folder)
2. Detects file creation events via FileSystemWatcher
3. Waits for file to become accessible (up to 10 seconds)
4. Moves file to the same game-specific subfolder as active captures
5. Useful for consolidating screenshots from games with native screenshot features

### Key Configuration

- **Enabled**: Toggle automatic screenshot capture
- **IntervalMinutes**: Time between screenshots (default: 30 minutes)
- **OutputFolder**: Directory for saved screenshots (defaults to `%AppData%\Playnite\Plugins\SharpMemories\Screenshots`)
- **MonitorFolder**: Optional folder to monitor for external screenshots (e.g., game's built-in screenshot directory)

## File Structure

```
SharpMemories/
├── SharpMemories.cs              # Main plugin implementation
├── ScreenshotCaptureManager.cs   # Screenshot capture lifecycle
├── FolderMonitorManager.cs       # External folder monitoring
├── ScreenCapture.cs              # Native Win32 screenshot utilities
├── FileHelpers.cs                # Filename sanitization utilities
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

## Architecture Notes

### Manager Pattern
The plugin uses a manager-based architecture where the main plugin class delegates to specialized managers:
- **ScreenshotCaptureManager**: Handles active screenshot capture with timing control
- **FolderMonitorManager**: Handles passive file system monitoring

Both managers are instantiated once at plugin initialization and reused across game sessions.

### Threading and Concurrency
- Screenshot capture runs on background Task with CancellationToken for clean shutdown
- Thread-safe locks protect capture state (captureCts, captureTask) in ScreenshotCaptureManager
- FileSystemWatcher events run on background thread; file operations handled with retry logic

### Resource Management
- Native GDI handles (HDC, HBITMAP) properly released in finally blocks
- CancellationTokenSource disposed after use
- FileSystemWatcher disposed when monitoring stops
- Bitmap objects disposed after saving

### Error Handling
- Extensive logging via Playnite's ILogger (Info, Debug, Warn, Error levels)
- Graceful degradation: window capture failures fall back to screen capture
- File locking handled with retry loop (100ms intervals, 10 second timeout)
- Null checks on settings and game data throughout