using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Inspection
{
    /// <summary>
    /// get_document_warnings — inspect all active warnings and unresolved failure messages in the Revit model.
    /// </summary>
    public class GetDocumentWarningsCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private GetDocumentWarningsEventHandler _handler => (GetDocumentWarningsEventHandler)Handler;

        public override string CommandName => "get_document_warnings";

        public GetDocumentWarningsCommand(UIApplication uiApp)
            : base(new GetDocumentWarningsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for get_document_warnings external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to retrieve document warnings: {ex.Message}", ex);
                }
            }
        }
    }
}
