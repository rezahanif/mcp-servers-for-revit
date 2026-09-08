using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Views;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Views
{
    public class AdjustSectionDatumsCommand : ExternalEventCommandBase
    {
        private AdjustSectionDatumsEventHandler _handler => (AdjustSectionDatumsEventHandler)Handler;

        public override string CommandName => "adjust_section_datums";

        public AdjustSectionDatumsCommand(UIApplication uiApp)
            : base(new AdjustSectionDatumsEventHandler(), uiApp)
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
                    throw new TimeoutException("Adjust section datums operation timed out");
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
