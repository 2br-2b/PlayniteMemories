using System;
using System.Collections.Generic;

namespace SharpMemories
{
    /// <summary>
    /// Represents a data wrapper for binding library plugin settings to the User Interface.
    /// This allows the user to enable/disable hotkeys for specific game libraries (e.g., Steam, Epic).
    /// </summary>
    public class LibraryPluginInfo : ObservableObject
    {
        #region Fields
        private bool _isHotkeyEnabled;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the unique identifier of the library plugin.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the library plugin.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the global hotkey should be active for games from this library.
        /// </summary>
        public bool IsHotkeyEnabled
        {
            get => _isHotkeyEnabled;
            set => SetValue(ref _isHotkeyEnabled, value);
        }
        #endregion
    }
}