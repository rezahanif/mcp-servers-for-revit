using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace revit_mcp_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class MCPServiceConnection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 获取socket服务
                // Obtain socket service.
                SocketService service = SocketService.Instance;

                if (service.IsRunning)
                {
                    service.Stop();
                    TaskDialog.Show("Revit MCP", "Revit MCP Server stopped.");
                }
                else
                {
                    service.Initialize(commandData.Application);
                    service.Start();
                    if (service.IsRunning)
                    {
                        TaskDialog.Show("Revit MCP", $"Revit MCP Server is running on port {service.Port}.");
                    }
                    else
                    {
                        string err = string.IsNullOrEmpty(service.LastError) 
                            ? "Port may be in use by another application." 
                            : service.LastError;
                        TaskDialog.Show("Revit MCP Error", $"Failed to start server on port {service.Port}.\n\nError: {err}\n\nPlease check if another program is using port {service.Port} or set REVIT_SOCKET_PORT.");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
