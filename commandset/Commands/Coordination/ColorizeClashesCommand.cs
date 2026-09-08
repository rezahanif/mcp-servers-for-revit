using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Coordination;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Coordination
{
    public class ColorizeClashesCommand : ExternalEventCommandBase
    {
        private ColorizeClashesEventHandler _handler => (ColorizeClashesEventHandler)Handler;

        public override string CommandName => "colorize_clashes";

        public ColorizeClashesCommand(UIApplication uiApp)
            : base(new ColorizeClashesEventHandler(), uiApp)
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
                    throw new TimeoutException("Colorize clashes operation timed out");
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
