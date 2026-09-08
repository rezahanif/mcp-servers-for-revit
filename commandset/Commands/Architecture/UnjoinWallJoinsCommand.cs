using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Architecture;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class UnjoinWallJoinsCommand : ExternalEventCommandBase
    {
        private UnjoinWallJoinsEventHandler _handler => (UnjoinWallJoinsEventHandler)Handler;
        public override string CommandName => "unjoin_wall_joins";

        public UnjoinWallJoinsCommand(UIApplication uiApp) : base(new UnjoinWallJoinsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            var wallIdsArray = parameters?["wallIds"] as JArray;
            List<int> wallIds = wallIdsArray != null ? wallIdsArray.Select(id => (int)id).ToList() : null;
            int? viewId = parameters?["viewId"]?.Value<int?>();

            _handler.SetParameters(wallIds, viewId);
            if (RaiseAndWaitForCompletion(30000)) return _handler.Result;
            throw new TimeoutException("unjoin_wall_joins timed out");
        }
    }
}
