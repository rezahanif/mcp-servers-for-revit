using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Architecture;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class RejoinWallJoinsCommand : ExternalEventCommandBase
    {
        private RejoinWallJoinsEventHandler _handler => (RejoinWallJoinsEventHandler)Handler;
        public override string CommandName => "rejoin_wall_joins";

        public RejoinWallJoinsCommand(UIApplication uiApp) : base(new RejoinWallJoinsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            _handler.SetParameters();
            if (RaiseAndWaitForCompletion(30000)) return _handler.Result;
            throw new TimeoutException("rejoin_wall_joins timed out");
        }
    }
}
