using RevitMCPCommandSet.Services.Architecture;
using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Architecture
{
    /// <summary>
    /// Command to trace assembled-stair geometry and detect occluded step edges in the active view
    /// </summary>
    public class TraceStairGeometryCommand : ExternalEventCommandBase
    {
        private TraceStairGeometryEventHandler _handler => (TraceStairGeometryEventHandler)Handler;

        public override string CommandName => "trace_stair_geometry";

        public TraceStairGeometryCommand(UIApplication uiApp)
            : base(new TraceStairGeometryEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();

                if (RaiseAndWaitForCompletion(20000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Trace stair geometry operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to trace stair geometry: {ex.Message}");
            }
        }
    }
}
