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
    /// Command to list loaded furniture types
    /// </summary>
    public class GetFurnitureTypesCommand : ExternalEventCommandBase
    {
        private GetFurnitureTypesEventHandler _handler => (GetFurnitureTypesEventHandler)Handler;

        public override string CommandName => "get_furniture_types";

        public GetFurnitureTypesCommand(UIApplication uiApp)
            : base(new GetFurnitureTypesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string category = parameters["category"]?.Value<string>();
                _handler.SetParameters(category);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get furniture types operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get furniture types: {ex.Message}");
            }
        }
    }
}
