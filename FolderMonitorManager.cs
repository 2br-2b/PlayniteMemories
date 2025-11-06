using Playnite.SDK;
using System;
using System.IO;
using System.Threading;

namespace SharpMemories
{
    public class FolderMonitorManager
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly SharpMemoriesSettingsViewModel settings;
        private FileSystemWatcher fileWatcher;
        private string currentGameTitle = null;

        public FolderMonitorManager(SharpMemoriesSettingsViewModel settings)
        {
            this.settings = settings;
        }

        public void StartMonitoring(string gameTitle)
        {
            var monitorFolder = settings?.Settings?.MonitorFolder;
            if (string.IsNullOrWhiteSpace(monitorFolder))
            {
                logger.Debug("Monitor folder is not configured, skipping folder monitoring");
                return;
            }

            logger.Info($"Starting folder monitor for: {monitorFolder}");

            if (fileWatcher != null)
            {
                logger.Debug("Disposing existing file watcher before creating new one");
                fileWatcher.Dispose();
            }

            currentGameTitle = gameTitle;

            fileWatcher = new FileSystemWatcher(monitorFolder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                Filter = "*.*", // Monitor all files
                EnableRaisingEvents = true
            };

            fileWatcher.Created += OnNewScreenshot;
            logger.Debug("File watcher created and event handler attached");
        }

        public void StopMonitoring()
        {
            if (fileWatcher != null)
            {
                logger.Info("Stopping folder monitor");
                fileWatcher.EnableRaisingEvents = false;
                fileWatcher.Dispose();
                fileWatcher = null;
                logger.Debug("File watcher disposed");
            }
            else
            {
                logger.Debug("No active file watcher to stop");
            }

            currentGameTitle = null;
        }

        private void OnNewScreenshot(object sender, FileSystemEventArgs e)
        {
            try
            {
                logger.Debug($"New file detected in monitor folder: {e.FullPath}");

                if (string.IsNullOrWhiteSpace(currentGameTitle))
                {
                    logger.Warn("No game is currently open. Ignoring new file.");
                    return;
                }

                var outFolder = settings?.Settings?.OutputFolder;
                if (string.IsNullOrWhiteSpace(outFolder))
                {
                    outFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite", "Plugins", "SharpMemories", "Screenshots");
                    logger.Debug($"Using default output folder: {outFolder}");
                }

                var safeTitle = FileHelpers.MakeSafeFilename(currentGameTitle);
                var gameFolder = Path.Combine(outFolder, safeTitle);

                try { Directory.CreateDirectory(gameFolder); } catch (Exception ex) { logger.Error(ex, "Failed to create game folder"); }

                var destinationPath = Path.Combine(gameFolder, Path.GetFileName(e.FullPath));

                // Wait for file to become available before attempting to move it
                if (!WaitForFileAccess(e.FullPath, maxWaitSeconds: 10))
                {
                    logger.Error($"Timeout waiting for file access: {e.FullPath}");
                    return;
                }

                File.Move(e.FullPath, destinationPath);

                logger.Info($"Moved file '{e.FullPath}' to '{destinationPath}'");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error handling new file: {e.FullPath}");
            }
        }

        private bool WaitForFileAccess(string filePath, int maxWaitSeconds = 10)
        {
            var maxAttempts = maxWaitSeconds * 10; // Check every 100ms
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    // Try to open the file exclusively to check if it's accessible
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        logger.Debug($"File is now accessible: {filePath} (after {i * 100}ms)");
                        return true;
                    }
                }
                catch (FileNotFoundException)
                {
                    logger.Warn($"File no longer exists: {filePath}");
                    return false;
                }
                catch (IOException)
                {
                    // File is still locked, wait and retry
                    if (i == 0)
                    {
                        logger.Debug($"File is locked, waiting for access: {filePath}");
                    }
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException)
                {
                    // Permission issue
                    logger.Error($"Access denied to file: {filePath}");
                    return false;
                }
            }

            logger.Warn($"Timeout waiting for file access after {maxWaitSeconds} seconds: {filePath}");
            return false;
        }
    }
}
