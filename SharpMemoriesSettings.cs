using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace SharpMemories
{
    // Helper class for UI binding to library plugin settings
    public class LibraryPluginInfo : ObservableObject
    {
        private bool isHotkeyEnabled;

        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsHotkeyEnabled
        {
            get => isHotkeyEnabled;
            set => SetValue(ref isHotkeyEnabled, value);
        }
    }

    public class SharpMemoriesSettings : ObservableObject
    {
        private bool enabled = true;
        private int intervalMinutes = 30;
        private string outputFolder = string.Empty;
        private string monitorFolder = string.Empty;

        // Hotkey settings
        private bool enableHotkey = false;
        private Key hotkeyKey = Key.F12;
        private bool hotkeyCtrl = false;
        private bool hotkeyAlt = false;
        private bool hotkeyShift = false;

        // Per-library hotkey enable flags - dynamic dictionary keyed by library plugin ID
        private Dictionary<Guid, bool> hotkeyEnabledByLibrary = new Dictionary<Guid, bool>();

        public bool Enabled { get => enabled; set => SetValue(ref enabled, value); }
        public int IntervalMinutes { get => intervalMinutes; set => SetValue(ref intervalMinutes, value); }
        public string OutputFolder { get => outputFolder; set => SetValue(ref outputFolder, value); }
        public string MonitorFolder { get => monitorFolder; set => SetValue(ref monitorFolder, value); }

        // Hotkey properties
        public bool EnableHotkey { get => enableHotkey; set => SetValue(ref enableHotkey, value); }
        public Key HotkeyKey { get => hotkeyKey; set => SetValue(ref hotkeyKey, value); }
        public bool HotkeyCtrl { get => hotkeyCtrl; set => SetValue(ref hotkeyCtrl, value); }
        public bool HotkeyAlt { get => hotkeyAlt; set => SetValue(ref hotkeyAlt, value); }
        public bool HotkeyShift { get => hotkeyShift; set => SetValue(ref hotkeyShift, value); }

        // Per-library hotkey enable property - exposed as dictionary
        public Dictionary<Guid, bool> HotkeyEnabledByLibrary
        {
            get => hotkeyEnabledByLibrary;
            set => SetValue(ref hotkeyEnabledByLibrary, value);
        }

        // Helper method to check if hotkey is enabled for a specific library
        public bool IsHotkeyEnabledForLibrary(Guid libraryId)
        {
            // Default to true for libraries not in the dictionary
            return hotkeyEnabledByLibrary.TryGetValue(libraryId, out bool enabled) ? enabled : true;
        }

        // Helper method to set hotkey enabled state for a specific library
        public void SetHotkeyEnabledForLibrary(Guid libraryId, bool enabled)
        {
            hotkeyEnabledByLibrary[libraryId] = enabled;
            OnPropertyChanged(nameof(HotkeyEnabledByLibrary));
        }
    }

    public class SharpMemoriesSettingsViewModel : ObservableObject, ISettings
    {
        private readonly SharpMemories plugin;
        private SharpMemoriesSettings editingClone { get; set; }

        private SharpMemoriesSettings settings;
        public SharpMemoriesSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        private List<LibraryPluginInfo> libraryPlugins;
        public List<LibraryPluginInfo> LibraryPlugins
        {
            get => libraryPlugins;
            set
            {
                libraryPlugins = value;
                OnPropertyChanged();
            }
        }

        public SharpMemoriesSettingsViewModel(SharpMemories plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<SharpMemoriesSettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new SharpMemoriesSettings();
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);

            // Populate library plugins list from Playnite API
            try
            {
                var plugins = plugin.PlayniteApi.Addons.Plugins.OfType<LibraryPlugin>().ToList();
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
                // Log error and provide empty list as fallback
                LogManager.GetLogger().Error(ex, "Failed to enumerate library plugins");
                LibraryPlugins = new List<LibraryPluginInfo>();
            }
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // Save the library plugin settings back to the dictionary
            if (LibraryPlugins != null)
            {
                foreach (var libraryPlugin in LibraryPlugins)
                {
                    Settings.SetHotkeyEnabledForLibrary(libraryPlugin.Id, libraryPlugin.IsHotkeyEnabled);
                }
            }

            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}