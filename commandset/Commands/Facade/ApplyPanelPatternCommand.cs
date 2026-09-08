using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Facade;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Facade
{
    public class ApplyPanelPatternCommand : ExternalEventCommandBase
    {
        private ApplyPanelPatternEventHandler _handler => (ApplyPanelPatternEventHandler)Handler;

        public override string CommandName => "apply_panel_pattern";

        public ApplyPanelPatternCommand(UIApplication uiApp)
            : base(new ApplyPanelPatternEventHandler(), uiApp)
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
                    throw new TimeoutException("Apply panel pattern operation timed out");
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
