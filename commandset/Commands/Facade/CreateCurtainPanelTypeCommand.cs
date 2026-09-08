using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Facade;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Facade
{
    public class CreateCurtainPanelTypeCommand : ExternalEventCommandBase
    {
        private CreateCurtainPanelTypeEventHandler _handler => (CreateCurtainPanelTypeEventHandler)Handler;

        public override string CommandName => "create_curtain_panel_type";

        public CreateCurtainPanelTypeCommand(UIApplication uiApp)
            : base(new CreateCurtainPanelTypeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters(parameters);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Create curtain panel type operation timed out");
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
