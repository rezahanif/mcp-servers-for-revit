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
    /// Command to get the list of all levels in the project
    /// </summary>
    public class GetAllLevelsCommand : ExternalEventCommandBase
    {
        private GetAllLevelsEventHandler _handler => (GetAllLevelsEventHandler)Handler;

        public override string CommandName => "get_all_levels";

        public GetAllLevelsCommand(UIApplication uiApp)
            : base(new GetAllLevelsEventHandler(), uiApp)
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
                    throw new TimeoutException("Get all levels operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get all levels: {ex.Message}");
            }
        }
    }
}
