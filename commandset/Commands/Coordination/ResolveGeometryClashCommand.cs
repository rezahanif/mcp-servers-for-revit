using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Coordination
{
    /// <summary>
    /// resolve_geometry_clash — resolve geometry collisions and overlaps using Revit's Cut Geometry or Join Geometry APIs.
    /// </summary>
    public class ResolveGeometryClashCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private ResolveGeometryClashEventHandler _handler => (ResolveGeometryClashEventHandler)Handler;

        public override string CommandName => "resolve_geometry_clash";

        public ResolveGeometryClashCommand(UIApplication uiApp)
            : base(new ResolveGeometryClashEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    _handler.ElementIdA = parameters?["elementIdA"]?.Value<long>()
                        ?? throw new ArgumentException("elementIdA is required");
                    _handler.ElementIdB = parameters?["elementIdB"]?.Value<long>()
                        ?? throw new ArgumentException("elementIdB is required");
                    _handler.Action = parameters?["action"]?.ToString() ?? "cut";

                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for resolve_geometry_clash external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to resolve geometry clash: {ex.Message}", ex);
                }
            }
        }
    }
}
