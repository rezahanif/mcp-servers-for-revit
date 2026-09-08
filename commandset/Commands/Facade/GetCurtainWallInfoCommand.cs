using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Facade;

namespace RevitMCPCommandSet.Commands.Facade
{
    /// <summary>
    /// Command to get detailed curtain wall information
    /// </summary>
    public class GetCurtainWallInfoCommand : ExternalEventCommandBase
    {
        private GetCurtainWallInfoEventHandler _handler => (GetCurtainWallInfoEventHandler)Handler;

        public override string CommandName => "get_curtain_wall_info";

        public GetCurtainWallInfoCommand(UIApplication uiApp)
            : base(new GetCurtainWallInfoEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                int? elementId = parameters["elementId"]?.Value<int?>();

                _handler.SetParameters(elementId);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get curtain wall info operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get curtain wall info: {ex.Message}");
            }
        }
    }
}
