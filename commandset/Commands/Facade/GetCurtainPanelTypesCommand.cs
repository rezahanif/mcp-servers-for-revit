using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Facade;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Facade
{
    public class GetCurtainPanelTypesCommand : ExternalEventCommandBase
    {
        private GetCurtainPanelTypesEventHandler _handler => (GetCurtainPanelTypesEventHandler)Handler;

        public override string CommandName => "get_curtain_panel_types";

        public GetCurtainPanelTypesCommand(UIApplication uiApp)
            : base(new GetCurtainPanelTypesEventHandler(), uiApp)
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
                    throw new TimeoutException("Get curtain panel types operation timed out");
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
