using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Views;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Views
{
    /// <summary>
    /// Command to set the active view in Revit.
    /// </summary>
    public class SetActiveViewCommand : ExternalEventCommandBase
    {
        private SetActiveViewEventHandler _handler => (SetActiveViewEventHandler)Handler;

        public override string CommandName => "set_active_view";

        public SetActiveViewCommand(UIApplication uiApp)
            : base(new SetActiveViewEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long viewId = parameters["viewId"]?.ToObject<long>() ?? 0;
                if (viewId <= 0)
                    throw new ArgumentException("A valid viewId is required");

                _handler.SetParameters(viewId);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Set active view operation timed out");
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
