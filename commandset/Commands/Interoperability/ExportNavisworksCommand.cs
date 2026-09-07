using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Interoperability
{
    public class ExportNavisworksCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private ExportNavisworksEventHandler _handler => (ExportNavisworksEventHandler)Handler;

        public override string CommandName => "export_navisworks";

        public ExportNavisworksCommand(UIApplication uiApp)
            : base(new ExportNavisworksEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    _handler.OutputFolder = parameters?["outputFolder"]?.ToString();
                    _handler.FileName = parameters?["fileName"]?.ToString();
                    _handler.ExportElementIds = parameters?["exportElementIds"]?.Value<bool>() ?? true;
                    _handler.ConvertLinkedCAD = parameters?["convertLinkedCAD"]?.Value<bool>() ?? true;

                    if (RaiseAndWaitForCompletion(35000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for export_navisworks external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to export Navisworks file: {ex.Message}", ex);
                }
            }
        }
    }
}
