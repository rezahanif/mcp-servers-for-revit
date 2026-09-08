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
    /// Command to change an element's (or a batch of elements') type
    /// </summary>
    public class ChangeElementTypeCommand : ExternalEventCommandBase
    {
        private ChangeElementTypeEventHandler _handler => (ChangeElementTypeEventHandler)Handler;

        public override string CommandName => "change_element_type";

        public ChangeElementTypeCommand(UIApplication uiApp)
            : base(new ChangeElementTypeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                int? elementId = parameters["elementId"]?.Value<int?>();
                var elementIdsToken = parameters["elementIds"] as JArray;
                var elementIds = elementIdsToken?.Select(t => t.Value<int>()).ToList();
                int typeId = parameters["typeId"]?.Value<int>() ?? 0;

                if (typeId == 0)
                    throw new ArgumentException("typeId is required");

                _handler.SetParameters(elementId, elementIds, typeId);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Change element type operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to change element type: {ex.Message}");
            }
        }
    }
}
