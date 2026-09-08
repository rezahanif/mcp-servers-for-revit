using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Interaction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Interaction
{
    /// <summary>
    /// Command to zoom the active view to an element.
    /// </summary>
    public class ZoomToElementCommand : ExternalEventCommandBase
    {
        private ZoomToElementEventHandler _handler => (ZoomToElementEventHandler)Handler;

        public override string CommandName => "zoom_to_element";

        public ZoomToElementCommand(UIApplication uiApp)
            : base(new ZoomToElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long elementId = parameters["elementId"]?.ToObject<long>() ?? 0;
                if (elementId <= 0)
                    throw new ArgumentException("A valid elementId is required");

                _handler.SetParameters(elementId);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Zoom to element operation timed out");
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
    }
}
