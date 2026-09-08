using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Architecture;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class JoinWallTopsCommand : ExternalEventCommandBase
    {
        private JoinWallTopsEventHandler _handler => (JoinWallTopsEventHandler)Handler;
        public override string CommandName => "join_wall_tops";

        public JoinWallTopsCommand(UIApplication uiApp) : base(new JoinWallTopsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            var levelsArray = parameters?["levels"] as JArray;
            List<string> levels = levelsArray != null ? levelsArray.Select(l => (string)l).ToList() : null;

            _handler.SetParameters(levels);
            if (RaiseAndWaitForCompletion(60000)) return _handler.Result;
            throw new TimeoutException("join_wall_tops timed out");
        }
    }
}
