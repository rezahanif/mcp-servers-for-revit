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
    /// Command to list available wall types
    /// </summary>
    public class GetWallTypesCommand : ExternalEventCommandBase
    {
        private GetWallTypesEventHandler _handler => (GetWallTypesEventHandler)Handler;

        public override string CommandName => "get_wall_types";

        public GetWallTypesCommand(UIApplication uiApp)
            : base(new GetWallTypesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string search = parameters["search"]?.Value<string>();
                _handler.SetParameters(search);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get wall types operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get wall types: {ex.Message}");
            }
        }
    }
}
