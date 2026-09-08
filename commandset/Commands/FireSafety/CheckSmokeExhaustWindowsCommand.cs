using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.FireSafety;
using RevitMCPCommandSet.Services.FireSafety;

namespace RevitMCPCommandSet.Commands.FireSafety
{
    /// <summary>
    /// Smoke-exhaust window compliance check (building-code §101 / fire-code §188).
    /// Ported from REVIT_MCP_study's CommandExecutor.CheckSmokeExhaustWindows.
    /// </summary>
    public class CheckSmokeExhaustWindowsCommand : ExternalEventCommandBase
    {
        private CheckSmokeExhaustWindowsEventHandler _handler => (CheckSmokeExhaustWindowsEventHandler)Handler;

        public override string CommandName => "check_smoke_exhaust_windows";

        public CheckSmokeExhaustWindowsCommand(UIApplication uiApp)
            : base(new CheckSmokeExhaustWindowsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            var request = parameters.ToObject<CheckSmokeExhaustWindowsRequest>();
            if (request == null || string.IsNullOrEmpty(request.LevelName))
                throw new System.ArgumentException("levelName is required");

            _handler.SetParameters(request);

            if (RaiseAndWaitForCompletion(20000))
                return _handler.Result;

            throw new System.TimeoutException("Smoke-exhaust window check timed out");
        }
    }
}
