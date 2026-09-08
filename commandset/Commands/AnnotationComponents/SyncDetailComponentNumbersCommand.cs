using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.AnnotationComponents;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.AnnotationComponents
{
    public class SyncDetailComponentNumbersCommand : ExternalEventCommandBase
    {
        private SyncDetailComponentNumbersEventHandler _handler => (SyncDetailComponentNumbersEventHandler)Handler;

        public override string CommandName => "sync_detail_component_numbers";

        public SyncDetailComponentNumbersCommand(UIApplication uiApp)
            : base(new SyncDetailComponentNumbersEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters(parameters);

                if (RaiseAndWaitForCompletion(20000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Sync detail component numbers operation timed out");
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
