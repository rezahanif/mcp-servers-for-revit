using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Coordination;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Coordination
{
    public class ExportClashReportCommand : ExternalEventCommandBase
    {
        private ExportClashReportEventHandler _handler => (ExportClashReportEventHandler)Handler;

        public override string CommandName => "export_clash_report";

        public ExportClashReportCommand(UIApplication uiApp)
            : base(new ExportClashReportEventHandler(), uiApp)
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
                    throw new TimeoutException("Export clash report operation timed out");
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
