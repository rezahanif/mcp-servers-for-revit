using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.MEP
{
    /// <summary>
    /// Event handler for get_connector_info — reads Connector (end-point) data off an
    /// MEP element (MEPCurve or a family instance with an MEPModel). Read-only, no Transaction.
    /// Ported from REVIT_MCP_study MCP/Core/CommandExecutor.cs GetConnectorInfo(JObject).
    /// </summary>
    public class GetConnectorInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private int _elementId;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<object> Result { get; private set; }

        public void SetParameters(int elementId)
        {
            _elementId = elementId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;
                Element element = doc.GetElement(new ElementId(_elementId));

                if (element == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = $"Element not found: {_elementId}",
                    };
                    return;
                }

                ConnectorSet connectors = null;
                if (element is MEPCurve curve)
                {
                    connectors = curve.ConnectorManager?.Connectors;
                }
                else if (element is FamilyInstance fi)
                {
                    connectors = fi.MEPModel?.ConnectorManager?.Connectors;
                }

                if (connectors == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = "This element has no connector information.",
                        Response = new { ElementId = _elementId, ElementName = element.Name, ConnectorCount = 0, Connectors = new List<object>() },
                    };
                    return;
                }

                var connectorList = new List<object>();
                foreach (Connector conn in connectors)
                {
                    connectorList.Add(new
                    {
                        ConnectorId = conn.Id,
                        Type = conn.ConnectorType.ToString(),
                        Origin = new
                        {
                            X = Math.Round(conn.Origin.X * 304.8, 2),
                            Y = Math.Round(conn.Origin.Y * 304.8, 2),
                            Z = Math.Round(conn.Origin.Z * 304.8, 2),
                        },
                        IsConnected = conn.IsConnected,
                        Shape = conn.Shape.ToString(),
                        Description = conn.Description,
                    });
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Found {connectorList.Count} connector(s).",
                    Response = new
                    {
                        ElementId = element.Id.GetIntValue(),
                        ElementName = element.Name,
                        ConnectorCount = connectorList.Count,
                        Connectors = connectorList,
                    },
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error getting connector info: {ex.Message}",
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Get Connector Info";
        }
    }
}
