using Playnite.SDK;
using System;
using System.IO;
using System.Linq;

namespace SharpMemories
{
    public static class SteamHelpers
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// Attempts to detect the Steam screenshot folder by finding the most recently used Steam user.
        /// Returns null if Steam is not found or no users exist.
        /// </summary>
        public static string GetSteamScreenshotFolder()
        {
            try
            {
                // Common Steam installation paths
                var steamPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
                };

                foreach (var steamPath in steamPaths)
                {
                    var userDataPath = Path.Combine(steamPath, "userdata");

                    if (!Directory.Exists(userDataPath))
                    {
                        continue;
                    }

                    // Get all user ID directories (they're numeric)
                    var userDirs = Directory.GetDirectories(userDataPath)
                        .Where(dir => long.TryParse(Path.GetFileName(dir), out _))
                        .Select(dir => new DirectoryInfo(dir))
                        .OrderByDescending(dir => dir.LastWriteTime)
                        .ToList();

                    if (userDirs.Count == 0)
                    {
                        continue;
                    }

                    // Use the most recently modified user directory
                    var mostRecentUser = userDirs.First();
                    var screenshotPath = Path.Combine(mostRecentUser.FullName, "760", "remote");

                    if (Directory.Exists(screenshotPath))
                    {
                        logger.Info($"Found Steam screenshot folder: {screenshotPath}");
                        return screenshotPath;
                    }
                }

                logger.Info("Steam installation found but no screenshot folders detected");
                return null;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to detect Steam screenshot folder");
                return null;
            }
        }

        /// <summary>
        /// Gets all Steam user screenshot folders, returning an array of paths.
        /// Useful if the user has multiple Steam accounts.
        /// </summary>
        public static string[] GetAllSteamScreenshotFolders()
        {
            try
            {
                var steamPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
                };

                foreach (var steamPath in steamPaths)
                {
                    var userDataPath = Path.Combine(steamPath, "userdata");

                    if (!Directory.Exists(userDataPath))
                    {
                        continue;
                    }

                    var screenshotFolders = Directory.GetDirectories(userDataPath)
                        .Where(dir => long.TryParse(Path.GetFileName(dir), out _))
                        .Select(dir => Path.Combine(dir, "760", "remote"))
                        .Where(path => Directory.Exists(path))
                        .ToArray();

                    if (screenshotFolders.Length > 0)
                    {
                        return screenshotFolders;
                    }
                }

                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to enumerate Steam screenshot folders");
                return Array.Empty<string>();
            }
        }
    }
}
