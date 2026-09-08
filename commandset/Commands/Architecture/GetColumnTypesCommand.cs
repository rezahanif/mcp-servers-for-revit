using RevitMCPCommandSet.Services.Architecture;
using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Architecture
{
    /// <summary>
    /// Command to get available column types
    /// </summary>
    public class GetColumnTypesCommand : ExternalEventCommandBase
    {
        private GetColumnTypesEventHandler _handler => (GetColumnTypesEventHandler)Handler;

        public override string CommandName => "get_column_types";

        public GetColumnTypesCommand(UIApplication uiApp)
            : base(new GetColumnTypesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string material = parameters["material"]?.Value<string>();
                _handler.SetParameters(material);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get column types operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get column types: {ex.Message}");
            }
        }
    }
}
