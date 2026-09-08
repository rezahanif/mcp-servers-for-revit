using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Materials;

namespace RevitMCPCommandSet.Commands.Materials
{
    /// <summary>Create a standalone Material with a specified RGB color (used for green-building/test materials).</summary>
    public class CreateGreenMaterialCommand : ExternalEventCommandBase
    {
        private CreateGreenMaterialEventHandler _handler => (CreateGreenMaterialEventHandler)Handler;
        public override string CommandName => "create_green_material";

        public CreateGreenMaterialCommand(UIApplication uiApp) : base(new CreateGreenMaterialEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            string materialName = parameters["materialName"]?.Value<string>();
            int r = parameters["r"]?.Value<int>() ?? 235;
            int g = parameters["g"]?.Value<int>() ?? 245;
            int b = parameters["b"]?.Value<int>() ?? 240;
            bool dryRun = parameters["dryRun"]?.Value<bool>() ?? false;
            if (string.IsNullOrEmpty(materialName))
                throw new System.ArgumentException("materialName is required");

            _handler.SetParameters(materialName, r, g, b, dryRun);
            if (RaiseAndWaitForCompletion(15000)) return _handler.Result;
            throw new System.TimeoutException("create_green_material timed out");
        }
    }
}
