using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Families
{
    public class LoadFamilyCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private LoadFamilyEventHandler _handler => (LoadFamilyEventHandler)Handler;

        public override string CommandName => "load_family";

        public LoadFamilyCommand(UIApplication uiApp)
            : base(new LoadFamilyEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    _handler.FilePath = parameters?["filePath"]?.ToString()
                        ?? throw new ArgumentException("filePath is required");
                    _handler.Overwrite = parameters?["overwrite"]?.Value<bool>() ?? false;

                    if (RaiseAndWaitForCompletion(20000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for load_family external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load family: {ex.Message}", ex);
                }
            }
        }
    }
}
