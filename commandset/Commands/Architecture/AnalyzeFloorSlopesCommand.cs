using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Architecture;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class AnalyzeFloorSlopesCommand : ExternalEventCommandBase
    {
        private AnalyzeFloorSlopesEventHandler _handler => (AnalyzeFloorSlopesEventHandler)Handler;

        public override string CommandName => "analyze_floor_slopes";

        public AnalyzeFloorSlopesCommand(UIApplication uiApp)
            : base(new AnalyzeFloorSlopesEventHandler(), uiApp)
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
                    throw new TimeoutException("Analyze floor slopes operation timed out");
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
