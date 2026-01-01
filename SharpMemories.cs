using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Windows.Controls;

namespace SharpMemories
{
    /// <summary>
    /// The main entry point for the SharpMemories Playnite plugin.
    /// Orchestrates screenshot capturing, folder monitoring, and hotkey management during game sessions.
    /// </summary>
    public class SharpMemories : GenericPlugin
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();

        // Service dependencies for core plugin functionality
        private readonly ScreenshotCaptureManager _screenshotCapture;
        private readonly FolderMonitorManager _folderMonitor;
        private readonly KeyboardHookManager _keyboardHook;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the unique identifier for this plugin.
        /// </summary>
        public override Guid Id { get; } = Guid.Parse("f6e5e286-47b0-4fa9-bc5d-2c17587d215d");

        /// <summary>
        /// Gets the settings view model containing user configuration.
        /// </summary>
        public SharpMemoriesSettingsViewModel SettingsViewModel { get; private set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="SharpMemories"/> class.
        /// Sets up the plugin environment and initializes sub-managers.
        /// </summary>
        /// <param name="api">The Playnite API instance.</param>
        public SharpMemories(IPlayniteAPI api) : base(api)
        {
            _logger.Info("Initializing SharpMemories plugin components...");

            SettingsViewModel = new SharpMemoriesSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            // Initialize worker classes with the current settings reference
            _screenshotCapture = new ScreenshotCaptureManager(SettingsViewModel);
            _folderMonitor = new FolderMonitorManager(SettingsViewModel);
            _keyboardHook = new KeyboardHookManager();

            _logger.Info("SharpMemories initialization complete.");
        }
        #endregion

        #region Game Events
        /// <summary>
        /// Triggered when a game starts. Initializes capture loops, monitors, and hotkeys.
        /// </summary>
        /// <param name="args">Event arguments containing game details.</param>
        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            try
            {
                var gameName = args.Game?.Name ?? "Unknown Game";
                var processId = GetProcessIdSafe(args);

                _logger.Info($"Game Started Event: {gameName} (PID: {processId})");

                if (!IsPluginEnabled())
                {
                    _logger.Info("Plugin is globally disabled in settings. Skipping activation.");
                    return;
                }

                // 1. Start the periodic screenshot capture loop
                InitializeScreenshotCapture(processId, gameName);

                // 2. Start monitoring the external screenshot folder (e.g., Steam folder)
                InitializeFolderMonitor(gameName);

                // 3. Register global hotkeys if applicable for this game
                InitializeHotkeys(args.Game, processId, gameName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Critical failure during OnGameStarted execution.");
            }
        }

        /// <summary>
        /// Triggered when a game stops. Cleans up all active monitors and hooks.
        /// </summary>
        /// <param name="args">Event arguments containing game details.</param>
        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            var gameName = args.Game?.Name ?? "Unknown Game";
            _logger.Info($"Game Stopped Event: {gameName}. Shutting down services.");

            // We wrap each shutdown call in individual try-catch blocks to ensure 
            // that a failure in one service doesn't prevent the others from cleaning up.

            try
            {
                _screenshotCapture.StopCapture();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to stop screenshot capture loop.");
            }

            try
            {
                _folderMonitor.StopMonitoring();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to stop folder monitor.");
            }

            try
            {
                _keyboardHook.UnregisterHotkey();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to unregister keyboard hotkey.");
            }
        }
        #endregion

        #region Application Events
        /// <summary>
        /// Triggered when Playnite shuts down. Ensures all native hooks and threads are released.
        /// </summary>
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            _logger.Info("Playnite application stopping. Releasing all SharpMemories resources.");

            try
            {
                _screenshotCapture?.StopCapture();
                _folderMonitor?.StopMonitoring();
                _keyboardHook?.Dispose();

                _logger.Info("Resource cleanup completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred during application shutdown cleanup.");
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Checks if the plugin is enabled in settings and settings are valid.
        /// </summary>
        private bool IsPluginEnabled()
        {
            return SettingsViewModel?.Settings != null && SettingsViewModel.Settings.Enabled;
        }

        /// <summary>
        /// Safely retrieves the process ID from event arguments, defaulting to 0 on failure.
        /// </summary>
        private int GetProcessIdSafe(OnGameStartedEventArgs args)
        {
            try
            {
                return args.StartedProcessId;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Configures and starts the periodic screenshot capture service.
        /// </summary>
        private void InitializeScreenshotCapture(int processId, string gameName)
        {
            if (string.IsNullOrWhiteSpace(SettingsViewModel.Settings.OutputFolder))
            {
                _logger.Warn("Output folder is not configured. Periodic screenshot capture will be disabled.");
                return;
            }

            _logger.Info($"Starting periodic capture service for '{gameName}'.");
            _screenshotCapture.StartCaptureForProcess(processId, gameName);
        }

        /// <summary>
        /// Configures and starts the file system watcher for external screenshots.
        /// </summary>
        private void InitializeFolderMonitor(string gameName)
        {
            if (SettingsViewModel.Settings.EnableMonitoring &&
                !string.IsNullOrWhiteSpace(SettingsViewModel.Settings.MonitorFolder))
            {
                _logger.Info($"Starting folder monitor for '{gameName}'.");
                _folderMonitor.StartMonitoring(gameName);
            }
        }

        /// <summary>
        /// Registers the global keyboard hotkey if enabled for the specific game library.
        /// </summary>
        private void InitializeHotkeys(Playnite.SDK.Models.Game game, int processId, string gameName)
        {
            if (ShouldEnableHotkeyForGame(game))
            {
                _logger.Info($"Registering capture hotkey for '{gameName}'.");

                _keyboardHook.RegisterHotkey(
                    SettingsViewModel.Settings.HotkeyKey,
                    SettingsViewModel.Settings.HotkeyCtrl,
                    SettingsViewModel.Settings.HotkeyAlt,
                    SettingsViewModel.Settings.HotkeyShift,
                    SettingsViewModel.Settings.HotkeySuppressKey,
                    () => _screenshotCapture.CaptureOnDemand(processId, gameName)
                );
            }
        }

        /// <summary>
        /// Determines if the hotkey should be active based on global settings and library-specific overrides.
        /// </summary>
        private bool ShouldEnableHotkeyForGame(Playnite.SDK.Models.Game game)
        {
            // First check global master switch
            if (!SettingsViewModel.Settings.EnableHotkey)
            {
                return false;
            }

            var pluginId = game?.PluginId ?? Guid.Empty;

            // Check if the specific library (Steam, Epic, etc.) allows hotkeys
            return SettingsViewModel.Settings.IsHotkeyEnabledForLibrary(pluginId);
        }
        #endregion

        #region Boilerplate Overrides
        public override void OnGameInstalled(OnGameInstalledEventArgs args) { }

        public override void OnGameStarting(OnGameStartingEventArgs args) { }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args) { }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args) { }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) { }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return SettingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SharpMemoriesSettingsView();
        }
        #endregion
    }
}