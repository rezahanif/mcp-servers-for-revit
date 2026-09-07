using System;
using System.Linq;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                string revitVersion = application.ControlledApplication.VersionNumber;
                int clrMajor = Environment.Version.Major;

                // Preflight runtime check
                if (revitVersion == "2026" && clrMajor < 10)
                {
                    TaskDialog.Show("Revit MCP Plugin Warning",
                        $"Warning: Revit 2026 runs on .NET 10 (CLR 10+), but detected CLR version {clrMajor}.\n" +
                        "This indicates a .NET 8 build of RevitMCPPlugin is installed.\n" +
                        "Please install the .NET 10 (Revit 2026) build to avoid assembly conflicts.");
                }
                else if (revitVersion == "2025" && (clrMajor != 8))
                {
                    TaskDialog.Show("Revit MCP Plugin Warning",
                        $"Warning: Revit 2025 runs on .NET 8, but detected CLR version {clrMajor}.\n" +
                        "Please verify the matching RevitMCPPlugin build is installed.");
                }
            }
            catch { }

            RibbonPanel mcpPanel = application.CreateRibbonPanel("Revit MCP Plugin");

            PushButtonData pushButtonData = new PushButtonData("ID_EXCMD_TOGGLE_REVIT_MCP", "Revit MCP\r\n Switch",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.MCPServiceConnection");
            pushButtonData.ToolTip = "Open / Close mcp server";
            pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/icon-16.png", UriKind.RelativeOrAbsolute));
            pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/icon-32.png", UriKind.RelativeOrAbsolute));
            mcpPanel.AddItem(pushButtonData);

            PushButtonData mcp_settings_pushButtonData = new PushButtonData("ID_EXCMD_MCP_SETTINGS", "Settings",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.Settings");
            mcp_settings_pushButtonData.ToolTip = "MCP Settings";
            mcp_settings_pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-16.png", UriKind.RelativeOrAbsolute));
            mcp_settings_pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-32.png", UriKind.RelativeOrAbsolute));
            mcpPanel.AddItem(mcp_settings_pushButtonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Stop();
                }
            }
            catch { }

            return Result.Succeeded;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var requestedName = new AssemblyName(args.Name).Name;
                if (requestedName == "RevitAPI" || requestedName == "RevitAPIUI")
                {
                    return AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == requestedName);
                }

                var loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == requestedName);
                if (loaded != null) return loaded;

                string pluginDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(pluginDir))
                {
                    string candidate = System.IO.Path.Combine(pluginDir, requestedName + ".dll");
                    if (System.IO.File.Exists(candidate))
                    {
                        return Assembly.LoadFrom(candidate);
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
