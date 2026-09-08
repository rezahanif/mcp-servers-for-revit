using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Facade;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Facade
{
    public class CreateFacadeFromAnalysisCommand : ExternalEventCommandBase
    {
        private CreateFacadeFromAnalysisEventHandler _handler => (CreateFacadeFromAnalysisEventHandler)Handler;

        public override string CommandName => "create_facade_from_analysis";

        public CreateFacadeFromAnalysisCommand(UIApplication uiApp)
            : base(new CreateFacadeFromAnalysisEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters(parameters);

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Create facade from analysis operation timed out");
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
