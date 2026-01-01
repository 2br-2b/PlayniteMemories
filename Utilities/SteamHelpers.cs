using Microsoft.Win32;
using Playnite.SDK;
using System;
using System.IO;
using System.Linq;

namespace SharpMemories
{
    /// <summary>
    /// Provides helper methods to locate Steam installation directories and user-specific screenshot folders.
    /// </summary>
    public static class SteamHelpers
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();
        #endregion

        #region Public Methods
        /// <summary>
        /// Attempts to detect the primary Steam screenshot folder by finding the most recently active Steam user.
        /// </summary>
        /// <returns>The full path to the screenshot folder, or null if not found.</returns>
        public static string GetSteamScreenshotFolder()
        {
            try
            {
                var steamPath = GetSteamInstallationPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    _logger.Debug("Steam installation path could not be found via Registry or Common Paths.");
                    return null;
                }

                var userDataPath = Path.Combine(steamPath, "userdata");
                if (!Directory.Exists(userDataPath))
                {
                    _logger.Debug($"Steam userdata directory missing at: {userDataPath}");
                    return null;
                }

                // Get all user ID directories (Steam IDs are numeric)
                // We order by LastWriteTime to guess which user is currently active
                var userDirs = Directory.GetDirectories(userDataPath)
                    .Where(dir => long.TryParse(Path.GetFileName(dir), out _))
                    .Select(dir => new DirectoryInfo(dir))
                    .OrderByDescending(dir => dir.LastWriteTime)
                    .ToList();

                if (userDirs.Count == 0)
                {
                    _logger.Debug("No user directories found in Steam userdata folder.");
                    return null;
                }

                // Check the most recently modified user directory first
                foreach (var userDir in userDirs)
                {
                    // "760" is the AppID for Steam Screenshots functionality
                    // "remote" contains the actual image files
                    var screenshotPath = Path.Combine(userDir.FullName, "760", "remote");

                    if (Directory.Exists(screenshotPath))
                    {
                        _logger.Info($"Detected active Steam screenshot folder: {screenshotPath}");
                        return screenshotPath;
                    }
                }

                _logger.Info("Steam user directories found, but no 'remote' screenshot folders exist inside them.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "An error occurred while attempting to detect the Steam screenshot folder.");
                return null;
            }
        }

        /// <summary>
        /// Retrieves all discovered Steam screenshot folders for every user on the local machine.
        /// Useful if multiple accounts are used on the same PC.
        /// </summary>
        /// <returns>An array of valid directory paths.</returns>
        public static string[] GetAllSteamScreenshotFolders()
        {
            try
            {
                var steamPath = GetSteamInstallationPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    return Array.Empty<string>();
                }

                var userDataPath = Path.Combine(steamPath, "userdata");
                if (!Directory.Exists(userDataPath))
                {
                    return Array.Empty<string>();
                }

                // Locate all directories that look like a Steam ID and contain a screenshot subfolder
                var screenshotFolders = Directory.GetDirectories(userDataPath)
                    .Where(dir => long.TryParse(Path.GetFileName(dir), out _)) // Filter for numeric User IDs
                    .Select(dir => Path.Combine(dir, "760", "remote"))       // Construct screenshot path
                    .Where(path => Directory.Exists(path))                   // Ensure path exists
                    .ToArray();

                if (screenshotFolders.Length > 0)
                {
                    _logger.Debug($"Found {screenshotFolders.Length} Steam screenshot directories.");
                    return screenshotFolders;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to enumerate Steam screenshot folders.");
            }

            return Array.Empty<string>();
        }
        #endregion

        #region Private Helper Methods
        /// <summary>
        /// Determines the Steam installation path by checking the Windows Registry, 
        /// then falling back to common default directories.
        /// </summary>
        /// <returns>The path to the Steam root directory, or null if not found.</returns>
        private static string GetSteamInstallationPath()
        {
            // 1. Try Registry Detection (Most reliable for custom install locations)
            string registryPath = GetPathFromRegistry();
            if (!string.IsNullOrEmpty(registryPath) && Directory.Exists(registryPath))
            {
                _logger.Debug($"Steam path found via Registry: {registryPath}");
                return registryPath;
            }

            // 2. Fallback to Common Paths
            var potentialPaths = new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
            };

            foreach (var path in potentialPaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "steam.exe")))
                {
                    _logger.Debug($"Steam path found via Common Paths: {path}");
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Queries the Windows Registry for the Valve Steam InstallPath key.
        /// Handles both 64-bit and 32-bit registry views.
        /// </summary>
        private static string GetPathFromRegistry()
        {
            try
            {
                // On 64-bit Windows, Steam (32-bit app) keys are in WOW6432Node
                // HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam -> InstallPath
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (key?.GetValue("InstallPath") is string path)
                    {
                        return path;
                    }
                }

                // Fallback for 32-bit Windows or if key location differs
                // HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam -> InstallPath
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key?.GetValue("InstallPath") is string path)
                    {
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to read Steam path from Registry.");
            }

            return null;
        }
        #endregion
    }
}
