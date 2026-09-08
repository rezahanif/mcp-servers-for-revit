using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Parameters;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Parameters
{
    public class LoadSharedParametersCommand : ExternalEventCommandBase
    {
        private LoadSharedParametersEventHandler _handler => (LoadSharedParametersEventHandler)Handler;

        public override string CommandName => "load_shared_parameters";

        public LoadSharedParametersCommand(UIApplication uiApp)
            : base(new LoadSharedParametersEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters(parameters);

                if (RaiseAndWaitForCompletion(25000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Load shared parameters operation timed out");
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
