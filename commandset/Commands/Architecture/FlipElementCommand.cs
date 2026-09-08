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
    /// Command to flip a family instance element (facing or hand)
    /// </summary>
    public class FlipElementCommand : ExternalEventCommandBase
    {
        private FlipElementEventHandler _handler => (FlipElementEventHandler)Handler;

        public override string CommandName => "flip_element";

        public FlipElementCommand(UIApplication uiApp)
            : base(new FlipElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                int elementId = parameters["elementId"]?.ToObject<int>() ?? 0;
                string flipType = parameters["flipType"]?.ToObject<string>() ?? "facing";

                if (elementId <= 0)
                    throw new ArgumentException("A valid elementId is required");

                _handler.SetParameters(elementId, flipType);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Flip element operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to flip element: {ex.Message}");
            }
        }
    }
}
