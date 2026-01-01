using Playnite.SDK;
using System;
using System.IO;
using System.Threading;

namespace SharpMemories
{
    /// <summary>
    /// Manages the monitoring of a specific folder for new screenshots or media files
    /// and orchestrates their organization into game-specific folders.
    /// </summary>
    public class FolderMonitorManager
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();
        private readonly SharpMemoriesSettingsViewModel _settings;
        private FileSystemWatcher _fileWatcher;

        // Volatile is not strictly necessary here due to low contention, 
        // but good practice if accessed by the watcher thread.
        private string _currentGameTitle = null;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="FolderMonitorManager"/> class.
        /// </summary>
        /// <param name="settings">The plugin settings view model.</param>
        public FolderMonitorManager(SharpMemoriesSettingsViewModel settings)
        {
            this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts monitoring the configured folder for new files associated with the specified game.
        /// </summary>
        /// <param name="gameTitle">The title of the game currently running.</param>
        public void StartMonitoring(string gameTitle)
        {
            var monitorFolder = _settings.Settings?.MonitorFolder;

            if (string.IsNullOrWhiteSpace(monitorFolder))
            {
                _logger.Debug("Monitor folder path is not configured. Folder monitoring will be skipped.");
                return;
            }

            if (!Directory.Exists(monitorFolder))
            {
                _logger.Warn($"The configured monitor folder does not exist: {monitorFolder}");
                return;
            }

            _logger.Info($"Initializing folder monitor for directory: {monitorFolder}");

            // Ensure we don't have a lingering watcher from a previous session
            StopMonitoring();

            _currentGameTitle = gameTitle;

            InitializeWatcher(monitorFolder);
        }

        /// <summary>
        /// Stops the file system watcher and releases resources.
        /// </summary>
        public void StopMonitoring()
        {
            if (_fileWatcher != null)
            {
                _logger.Info("Stopping active folder monitor.");

                // Unsubscribe to prevent memory leaks or events firing during disposal
                _fileWatcher.Created -= OnNewFileDetected;
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            _currentGameTitle = null;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Sets up the FileSystemWatcher instance with specific filters.
        /// </summary>
        /// <param name="path">The directory path to watch.</param>
        private void InitializeWatcher(string path)
        {
            try
            {
                _fileWatcher = new FileSystemWatcher(path)
                {
                    // Watch for file creation specifically. 
                    // Note: 'LastWrite' can trigger multiple times during a single file copy, so we focus on creation/name.
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    Filter = "*.*",
                    EnableRaisingEvents = true
                };

                _fileWatcher.Created += OnNewFileDetected;
                _logger.Debug("FileSystemWatcher successfully started.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize FileSystemWatcher.");
            }
        }

        /// <summary>
        /// Handles the event when a new file is created in the monitored folder.
        /// </summary>
        private void OnNewFileDetected(object sender, FileSystemEventArgs e)
        {
            try
            {
                _logger.Debug($"New file detected by watcher: {e.FullPath}");

                if (string.IsNullOrWhiteSpace(_currentGameTitle))
                {
                    _logger.Warn("A file was detected, but no game title is currently set. Ignoring file.");
                    return;
                }

                // Resolve the output directory path
                var outputBaseFolder = GetOutputFolder();

                // Sanitize the game title to ensure it is valid for a folder name
                var safeGameTitle = FileHelpers.MakeSafeFilename(_currentGameTitle);

                var finalGameFolder = Path.Combine(outputBaseFolder, safeGameTitle);
                var destinationPath = Path.Combine(finalGameFolder, e.Name); // e.Name includes filename and extension

                // Delegate the actual heavy lifting (waiting and moving) to the helper
                FileHelpers.MoveFileSafe(e.FullPath, destinationPath);
            }
            catch (Exception ex)
            {
                // Catch-all to ensure the watcher thread doesn't crash the application
                _logger.Error(ex, $"Unexpected error handling new file: {e.FullPath}");
            }
        }

        /// <summary>
        /// Retrieves the user-configured output folder or falls back to the default plugin directory.
        /// </summary>
        /// <returns>The full path to the output directory.</returns>
        private string GetOutputFolder()
        {
            var configuredFolder = _settings.Settings?.OutputFolder;

            if (!string.IsNullOrWhiteSpace(configuredFolder))
            {
                return configuredFolder;
            }

            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Playnite",
                "Plugins",
                "SharpMemories",
                "Screenshots");

            _logger.Debug($"Output folder not configured. Using default: {defaultPath}");
            return defaultPath;
        }
        #endregion
    }
}
