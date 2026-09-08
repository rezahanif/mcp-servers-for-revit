using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.AnnotationComponents;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.AnnotationComponents
{
    public class DedupDetailElementsInViewCommand : ExternalEventCommandBase
    {
        private DedupDetailElementsInViewEventHandler _handler => (DedupDetailElementsInViewEventHandler)Handler;

        public override string CommandName => "dedup_detail_elements_in_view";

        public DedupDetailElementsInViewCommand(UIApplication uiApp)
            : base(new DedupDetailElementsInViewEventHandler(), uiApp)
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
                    throw new TimeoutException("Dedup detail elements operation timed out");
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
