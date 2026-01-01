using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace SharpMemories
{
    /// <summary>
    /// Specifies the file format for saved screenshots.
    /// </summary>
    public enum ScreenshotFormat
    {
        Png,
        Jpeg
    }

    /// <summary>
    /// Contains all persistent configuration data for the SharpMemories plugin.
    /// </summary>
    public class SharpMemoriesSettings : ObservableObject
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();

        // General settings
        private bool _enabled = true;
        private int _intervalMinutes = 15;
        private string _outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Playnite");

        // Monitoring settings
        private string _monitorFolder = string.Empty;
        private bool _enableMonitoring = true;

        // Image format settings
        private ScreenshotFormat _screenshotFormat = ScreenshotFormat.Png;
        private long _jpegQuality = 85;

        // Hotkey settings
        private bool _enableHotkey = true;
        private Key _hotkeyKey = Key.F12;
        private bool _hotkeyCtrl = false;
        private bool _hotkeyAlt = false;
        private bool _hotkeyShift = false;
        private bool _hotkeySuppressKey = true;

        // Per-library hotkey configuration
        private Dictionary<Guid, bool> _hotkeyEnabledByLibrary = new Dictionary<Guid, bool>();
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets a value indicating whether the plugin's core functionality is enabled.
        /// </summary>
        public bool Enabled { get => _enabled; set => SetValue(ref _enabled, value); }

        /// <summary>
        /// Gets or sets the interval (in minutes) for automatic screenshots.
        /// </summary>
        public int IntervalMinutes { get => _intervalMinutes; set => SetValue(ref _intervalMinutes, value); }

        /// <summary>
        /// Gets or sets the directory where screenshots will be saved.
        /// </summary>
        public string OutputFolder { get => _outputFolder; set => SetValue(ref _outputFolder, value); }

        /// <summary>
        /// Gets or sets the directory to watch for external screenshots (e.g., Steam folder).
        /// </summary>
        public string MonitorFolder { get => _monitorFolder; set => SetValue(ref _monitorFolder, value); }

        /// <summary>
        /// Gets or sets a value indicating whether the folder monitor is active.
        /// </summary>
        public bool EnableMonitoring { get => _enableMonitoring; set => SetValue(ref _enableMonitoring, value); }

        /// <summary>
        /// Gets or sets the file format for saved screenshots.
        /// </summary>
        public ScreenshotFormat ScreenshotFormat
        {
            get => _screenshotFormat;
            set => SetValue(ref _screenshotFormat, value, nameof(ScreenshotFormat), nameof(IsJpegFormat));
        }

        /// <summary>
        /// Helper property for UI binding visibility triggers.
        /// </summary>
        public bool IsJpegFormat => ScreenshotFormat == ScreenshotFormat.Jpeg;

        /// <summary>
        /// Gets or sets the compression quality for JPEG images (0-100).
        /// </summary>
        public long JpegQuality { get => _jpegQuality; set => SetValue(ref _jpegQuality, value); }

        /// <summary>
        /// Gets or sets a value indicating whether the global capture hotkey is enabled.
        /// </summary>
        public bool EnableHotkey { get => _enableHotkey; set => SetValue(ref _enableHotkey, value); }

        /// <summary>
        /// Gets or sets the primary key for the hotkey combination.
        /// </summary>
        public Key HotkeyKey { get => _hotkeyKey; set => SetValue(ref _hotkeyKey, value); }

        /// <summary>
        /// Gets or sets whether the Control key is required for the hotkey.
        /// </summary>
        public bool HotkeyCtrl { get => _hotkeyCtrl; set => SetValue(ref _hotkeyCtrl, value); }

        /// <summary>
        /// Gets or sets whether the Alt key is required for the hotkey.
        /// </summary>
        public bool HotkeyAlt { get => _hotkeyAlt; set => SetValue(ref _hotkeyAlt, value); }

        /// <summary>
        /// Gets or sets whether the Shift key is required for the hotkey.
        /// </summary>
        public bool HotkeyShift { get => _hotkeyShift; set => SetValue(ref _hotkeyShift, value); }

        /// <summary>
        /// Gets or sets whether the hotkey input should be suppressed from reaching the game.
        /// </summary>
        public bool HotkeySuppressKey { get => _hotkeySuppressKey; set => SetValue(ref _hotkeySuppressKey, value); }

        /// <summary>
        /// Gets or sets the dictionary containing per-library hotkey preferences.
        /// </summary>
        public Dictionary<Guid, bool> HotkeyEnabledByLibrary
        {
            get => _hotkeyEnabledByLibrary;
            set => SetValue(ref _hotkeyEnabledByLibrary, value);
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Determines if the hotkey should be active for a specific game library.
        /// </summary>
        /// <param name="libraryId">The unique ID of the library plugin.</param>
        /// <returns>True if enabled; otherwise, false.</returns>
        public bool IsHotkeyEnabledForLibrary(Guid libraryId)
        {
            // If the user explicitly configured this library, return that value.
            if (_hotkeyEnabledByLibrary.TryGetValue(libraryId, out bool enabled))
            {
                return enabled;
            }

            // Default behavior: Disable for Steam (to avoid conflicts with Steam's native F12), enable for others.
            // Steam Library ID: CB91DFC9-B977-43BF-8E70-55F46E410FAB
            return libraryId != Guid.Parse("CB91DFC9-B977-43BF-8E70-55F46E410FAB");
        }

        /// <summary>
        /// Updates the enablement state for a specific library.
        /// </summary>
        /// <param name="libraryId">The unique ID of the library plugin.</param>
        /// <param name="enabled">The new state.</param>
        public void SetHotkeyEnabledForLibrary(Guid libraryId, bool enabled)
        {
            _hotkeyEnabledByLibrary[libraryId] = enabled;
            OnPropertyChanged(nameof(HotkeyEnabledByLibrary));
        }

        /// <summary>
        /// Generates a human-readable string representation of the configured hotkey.
        /// </summary>
        public string GetHotkeyDisplayString()
        {
            var parts = new List<string>();
            if (_hotkeyCtrl) parts.Add("Ctrl");
            if (_hotkeyAlt) parts.Add("Alt");
            if (_hotkeyShift) parts.Add("Shift");
            parts.Add(_hotkeyKey.ToString());

            return string.Join(" + ", parts);
        }
        #endregion
    }
}