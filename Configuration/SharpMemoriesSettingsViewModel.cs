using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace SharpMemories
{
    /// <summary>
    /// ViewModel for the SharpMemories settings view.
    /// Handles loading, saving, and validating settings, as well as UI interaction logic.
    /// </summary>
    public class SharpMemoriesSettingsViewModel : ObservableObject, ISettings
    {
        #region Fields
        private readonly SharpMemories _plugin;
        private SharpMemoriesSettings _editingClone;
        private SharpMemoriesSettings _settings;
        private List<LibraryPluginInfo> _libraryPlugins;
        private bool _isRecordingHotkey = false;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the current settings object.
        /// </summary>
        public SharpMemoriesSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HotkeyDisplayString));
            }
        }

        /// <summary>
        /// Gets or sets the list of installed library plugins for the settings UI.
        /// </summary>
        public List<LibraryPluginInfo> LibraryPlugins
        {
            get => _libraryPlugins;
            set
            {
                _libraryPlugins = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the UI is currently listening for keyboard input to set the hotkey.
        /// </summary>
        public bool IsRecordingHotkey
        {
            get => _isRecordingHotkey;
            set
            {
                _isRecordingHotkey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordButtonText));
            }
        }

        /// <summary>
        /// Gets the text to display on the hotkey record button.
        /// </summary>
        public string RecordButtonText => IsRecordingHotkey ? "Press a key combination..." : "Record Hotkey";

        /// <summary>
        /// Gets a formatted string representation of the current hotkey combination.
        /// </summary>
        public string HotkeyDisplayString => Settings?.GetHotkeyDisplayString() ?? "None";
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="SharpMemoriesSettingsViewModel"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance.</param>
        public SharpMemoriesSettingsViewModel(SharpMemories plugin)
        {
            _plugin = plugin;

            // Load saved settings or initialize defaults
            var savedSettings = plugin.LoadPluginSettings<SharpMemoriesSettings>();

            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new SharpMemoriesSettings();

                // Attempt to auto-detect the Steam screenshot folder for better out-of-the-box experience
                var steamFolder = SteamHelpers.GetSteamScreenshotFolder();
                Settings.MonitorFolder = steamFolder ?? string.Empty;
            }
        }
        #endregion

        #region ISettings Implementation
        /// <summary>
        /// Called when the user opens the settings menu.
        /// Prepares the view by creating a clone for cancellation support and populating dynamic lists.
        /// </summary>
        public void BeginEdit()
        {
            _editingClone = Serialization.GetClone(Settings);

            // Populate the list of library plugins so the user can toggle hotkeys per library
            try
            {
                var plugins = _plugin.PlayniteApi.Addons.Plugins.OfType<LibraryPlugin>().ToList();

                LibraryPlugins = plugins
                    .OrderBy(p => p.Name)
                    .Select(p => new LibraryPluginInfo
                    {
                        Id = p.Id,
                        Name = p.Name,
                        IsHotkeyEnabled = Settings.IsHotkeyEnabledForLibrary(p.Id)
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error(ex, "Failed to enumerate installed library plugins.");
                LibraryPlugins = new List<LibraryPluginInfo>();
            }
        }

        /// <summary>
        /// Called when the user cancels editing. Reverts settings to the state before BeginEdit.
        /// </summary>
        public void CancelEdit()
        {
            Settings = _editingClone;
        }

        /// <summary>
        /// Called when the user saves settings. Persists changes to disk.
        /// </summary>
        public void EndEdit()
        {
            // Sync the UI list state back to the Settings dictionary
            if (LibraryPlugins != null)
            {
                foreach (var libraryPlugin in LibraryPlugins)
                {
                    Settings.SetHotkeyEnabledForLibrary(libraryPlugin.Id, libraryPlugin.IsHotkeyEnabled);
                }
            }

            _plugin.SavePluginSettings(Settings);
        }

        /// <summary>
        /// Validates the settings before saving.
        /// </summary>
        /// <param name="errors">A list of error messages to display to the user.</param>
        /// <returns>True if settings are valid; otherwise, false.</returns>
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (Settings.JpegQuality < 0 || Settings.JpegQuality > 100)
            {
                errors.Add("JPEG Quality must be between 0 and 100.");
                return false;
            }

            return true;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Updates the hotkey configuration from UI input.
        /// </summary>
        /// <param name="key">The main key pressed.</param>
        /// <param name="ctrl">Whether Ctrl was held.</param>
        /// <param name="alt">Whether Alt was held.</param>
        /// <param name="shift">Whether Shift was held.</param>
        public void UpdateHotkey(Key key, bool ctrl, bool alt, bool shift)
        {
            Settings.HotkeyKey = key;
            Settings.HotkeyCtrl = ctrl;
            Settings.HotkeyAlt = alt;
            Settings.HotkeyShift = shift;

            OnPropertyChanged(nameof(HotkeyDisplayString));
        }
        #endregion
    }
}