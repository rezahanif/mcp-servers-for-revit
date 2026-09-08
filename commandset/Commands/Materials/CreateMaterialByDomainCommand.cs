using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Materials;

namespace RevitMCPCommandSet.Commands.Materials
{
    /// <summary>Create a pure standalone Material (no prefixing, no default-wall base) with a fixed light color.</summary>
    public class CreateMaterialByDomainCommand : ExternalEventCommandBase
    {
        private CreateMaterialByDomainEventHandler _handler => (CreateMaterialByDomainEventHandler)Handler;
        public override string CommandName => "create_material_by_domain";

        public CreateMaterialByDomainCommand(UIApplication uiApp) : base(new CreateMaterialByDomainEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            string materialName = parameters["materialName"]?.Value<string>();
            bool dryRun = parameters["dryRun"]?.Value<bool>() ?? false;
            if (string.IsNullOrEmpty(materialName))
                throw new System.ArgumentException("materialName is required");

            _handler.SetParameters(materialName, dryRun);
            if (RaiseAndWaitForCompletion(15000)) return _handler.Result;
            throw new System.TimeoutException("create_material_by_domain timed out");
        }
    }
}
