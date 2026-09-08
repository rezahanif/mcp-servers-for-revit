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
    /// Command to inventory the project's MEP pipe segment and duct size catalogs (read-only)
    /// </summary>
    public class GetMepSegmentsAndSizesCommand : ExternalEventCommandBase
    {
        private GetMepSegmentsAndSizesEventHandler _handler => (GetMepSegmentsAndSizesEventHandler)Handler;

        public override string CommandName => "get_mep_segments_and_sizes";

        public GetMepSegmentsAndSizesCommand(UIApplication uiApp)
            : base(new GetMepSegmentsAndSizesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters(parameters);

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get MEP segments and sizes operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get MEP segments and sizes: {ex.Message}");
            }
        }
    }
}
