using Playnite.SDK;
using System;
using System.IO;
using System.Threading;

namespace SharpMemories
{
    /// <summary>
    /// Provides helper methods for file system operations, specifically handling file locking and safe movement.
    /// </summary>
    public static class FileHelpers
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();
        #endregion

        #region Public Methods
        public static string MakeSafeFilename(string name)
        {
            // Remove all invalid chars for a cleaner name
            var arr = name.ToCharArray();
            arr = Array.FindAll<char>(arr, (c => Array.IndexOf<char>(Path.GetInvalidFileNameChars(), c) < 0));
            return new string(arr);
        }

        /// <summary>
        /// Attempts to move a file to a destination, waiting for the file to be released by other processes if necessary.
        /// </summary>
        /// <param name="sourceFilePath">The full path of the source file.</param>
        /// <param name="destinationPath">The full path of the destination file.</param>
        /// <param name="maxWaitSeconds">The maximum time to wait for the file to become accessible (in seconds).</param>
        /// <returns>True if the file was moved successfully; otherwise, false.</returns>
        public static bool MoveFileSafe(string sourceFilePath, string destinationPath, int maxWaitSeconds = 10)
        {
            // External capture software (like OBS or Steam) often keeps the file handle open 
            // while writing the data. We must ensure the write is complete before moving.
            if (!WaitForFileAccess(sourceFilePath, maxWaitSeconds))
            {
                _logger.Error($"Operation aborted: Timeout waiting for exclusive access to '{sourceFilePath}'.");
                return false;
            }

            try
            {
                // Ensure the target directory exists before moving
                var directory = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(sourceFilePath, destinationPath);
                _logger.Info($"Successfully moved file from '{sourceFilePath}' to '{destinationPath}'.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to move file from '{sourceFilePath}' to '{destinationPath}'.");
                return false;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Polls a file until it can be opened exclusively, indicating that other processes have finished writing to it.
        /// </summary>
        /// <param name="filePath">The path of the file to check.</param>
        /// <param name="maxWaitSeconds">Maximum duration to wait.</param>
        /// <returns>True if access is obtained; otherwise, false.</returns>
        private static bool WaitForFileAccess(string filePath, int maxWaitSeconds)
        {
            var maxAttempts = maxWaitSeconds * 10; // 10 checks per second (100ms interval)

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    // Attempt to open the file with exclusive access. 
                    // If this succeeds, no other process is currently writing to it.
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        if (i > 0)
                        {
                            _logger.Debug($"File became accessible after {(i * 100)}ms: {filePath}");
                        }
                        return true;
                    }
                }
                catch (FileNotFoundException)
                {
                    _logger.Warn($"File vanished during wait routine: {filePath}");
                    return false;
                }
                catch (IOException)
                {
                    // The file is still locked by another process (e.g., the game or capture software).
                    // We wait a short period before retrying.
                    if (i == 0)
                    {
                        _logger.Debug($"File is currently locked. Waiting for release: {filePath}");
                    }
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.Error($"Permission denied for file: {filePath}");
                    return false;
                }
            }

            _logger.Warn($"Timeout reached ({maxWaitSeconds}s). File remains locked: {filePath}");
            return false;
        }
        #endregion
    }
}
