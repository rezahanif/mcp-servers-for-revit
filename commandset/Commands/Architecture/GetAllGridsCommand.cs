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
    /// Command to get all grid lines in the project
    /// </summary>
    public class GetAllGridsCommand : ExternalEventCommandBase
    {
        private GetAllGridsEventHandler _handler => (GetAllGridsEventHandler)Handler;

        public override string CommandName => "get_all_grids";

        public GetAllGridsCommand(UIApplication uiApp)
            : base(new GetAllGridsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get all grids operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get grids: {ex.Message}");
            }
        }
    }
}
