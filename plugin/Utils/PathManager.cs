using System.IO;

namespace revit_mcp_plugin.Utils
{
    public static class PathManager
    {
        /// <summary>
        /// Gets the root application data directory
        /// </summary>
        public static string GetAppDataDirectoryPath()
        {
            string applicationPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string applicationDirectory = Path.GetDirectoryName(applicationPath);

            return applicationDirectory;
        }
        /// <summary>
        /// Gets the path to the Commands directory
        /// </summary>
        public static string GetCommandsDirectoryPath()
        {
            string appDataDirectory = GetAppDataDirectoryPath();
            string commandsDirectory = Path.Combine(appDataDirectory, "Commands");

            EnsureDirectoryExists(commandsDirectory);

            return commandsDirectory;
        }
        /// <summary>
        /// Gets the path to the Logs directory
        /// </summary>
        public static string GetLogsDirectoryPath()
        {
            string appDataDirectory = GetAppDataDirectoryPath();
            string logsDirectory = Path.Combine(appDataDirectory, "Logs");

            EnsureDirectoryExists(logsDirectory);

            return logsDirectory;
        }
        /// <summary>
        /// Gets the path to the command registry file.
        /// </summary>
        /// <param name="createIfNotExists">
        /// When true (default) and the file is missing, throws instead of
        /// silently fabricating an empty one. See remarks.
        /// </param>
        /// <returns>Path to the command registry file</returns>
        /// <remarks>
        /// This used to auto-create an empty <c>{"commands": []}</c> stub
        /// whenever the real file was missing, and swallow any write
        /// failure behind a bare <c>Console.WriteLine</c> that never reached
        /// the plugin's actual log file. The shipped connector package
        /// always includes a populated commandRegistry.json (24+ commands),
        /// so a missing file means the package was not extracted/installed
        /// correctly — never a legitimate "fresh install, zero commands"
        /// state. Silently substituting an empty registry made every
        /// Revit-API-backed MCP tool fail with "Method 'X' not found" while
        /// the log's own "Command loading complete" line reported success,
        /// which cost a full investigation to diagnose (AiConnect gateway
        /// repo, docs/audit/BUG-LOG.md, N57). Throwing here instead routes
        /// through `MCPServiceConnection.Execute`'s existing catch block,
        /// which already surfaces `ex.Message` to the user as a Revit
        /// dialog — no new plumbing needed, just no longer hiding it.
        /// </remarks>
        public static string GetCommandRegistryFilePath(bool createIfNotExists = true)
        {
            string commandsDirectory = GetCommandsDirectoryPath();
            string registryFilePath = Path.Combine(commandsDirectory, "commandRegistry.json");

            if (createIfNotExists && !File.Exists(registryFilePath))
            {
                throw new FileNotFoundException(
                    $"commandRegistry.json is missing at '{registryFilePath}'. This means the " +
                    "revit-mcp connector package was not extracted/installed correctly — the " +
                    "shipped package always includes a populated command registry. Reinstall " +
                    "the connector; do not continue with zero commands registered.",
                    registryFilePath);
            }

            return registryFilePath;
        }
        /// <summary>
        /// Ensures that the specified directory exists
        /// </summary>
        /// <param name="directoryPath">The path to check and create if needed</param>
        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}
