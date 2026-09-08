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
    /// Command to move an element by a dx/dy/dz displacement (mm)
    /// </summary>
    public class MoveElementCommand : ExternalEventCommandBase
    {
        private MoveElementEventHandler _handler => (MoveElementEventHandler)Handler;

        public override string CommandName => "move_element";

        public MoveElementCommand(UIApplication uiApp)
            : base(new MoveElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                int elementId = parameters["elementId"]?.ToObject<int>() ?? 0;
                double dx = parameters["dx"]?.ToObject<double>() ?? 0;
                double dy = parameters["dy"]?.ToObject<double>() ?? 0;
                double dz = parameters["dz"]?.ToObject<double>() ?? 0;

                if (elementId <= 0)
                    throw new ArgumentException("A valid elementId is required");

                _handler.SetParameters(elementId, dx, dy, dz);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Move element operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to move element: {ex.Message}");
            }
        }
    }
}
