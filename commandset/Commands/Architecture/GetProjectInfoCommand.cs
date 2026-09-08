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
    /// Command to get basic information about the current Revit project
    /// </summary>
    public class GetProjectInfoCommand : ExternalEventCommandBase
    {
        private GetProjectInfoEventHandler _handler => (GetProjectInfoEventHandler)Handler;

        public override string CommandName => "get_project_info";

        public GetProjectInfoCommand(UIApplication uiApp)
            : base(new GetProjectInfoEventHandler(), uiApp)
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
                    throw new TimeoutException("Get project info operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get project info: {ex.Message}");
            }
        }
    }
}
