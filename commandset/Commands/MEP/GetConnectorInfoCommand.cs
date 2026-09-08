using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.MEP;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.MEP
{
    /// <summary>
    /// Command to get connector (pipe/duct/conduit end-point) info for an MEP element
    /// </summary>
    public class GetConnectorInfoCommand : ExternalEventCommandBase
    {
        private GetConnectorInfoEventHandler _handler => (GetConnectorInfoEventHandler)Handler;

        public override string CommandName => "get_connector_info";

        public GetConnectorInfoCommand(UIApplication uiApp)
            : base(new GetConnectorInfoEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                int elementId = parameters["elementId"]?.Value<int>() ?? 0;

                _handler.SetParameters(elementId);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get connector info operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get connector info: {ex.Message}");
            }
        }
    }
}
