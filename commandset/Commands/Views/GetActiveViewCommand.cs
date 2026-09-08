using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Views;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Views
{
    /// <summary>
    /// Command to get the currently active view in Revit.
    /// </summary>
    public class GetActiveViewCommand : ExternalEventCommandBase
    {
        private GetActiveViewEventHandler _handler => (GetActiveViewEventHandler)Handler;

        public override string CommandName => "get_active_view";

        public GetActiveViewCommand(UIApplication uiApp)
            : base(new GetActiveViewEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get active view operation timed out");
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
