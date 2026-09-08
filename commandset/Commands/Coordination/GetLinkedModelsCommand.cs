using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Coordination;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Coordination
{
    public class GetLinkedModelsCommand : ExternalEventCommandBase
    {
        private GetLinkedModelsEventHandler _handler => (GetLinkedModelsEventHandler)Handler;

        public override string CommandName => "get_linked_models";

        public GetLinkedModelsCommand(UIApplication uiApp)
            : base(new GetLinkedModelsEventHandler(), uiApp)
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
                    throw new TimeoutException("Get linked models operation timed out");
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
