using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Facade;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Facade
{
    public class CreateFacadePanelCommand : ExternalEventCommandBase
    {
        private CreateFacadePanelEventHandler _handler => (CreateFacadePanelEventHandler)Handler;

        public override string CommandName => "create_facade_panel";

        public CreateFacadePanelCommand(UIApplication uiApp)
            : base(new CreateFacadePanelEventHandler(), uiApp)
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
                    throw new TimeoutException("Create facade panel operation timed out");
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
