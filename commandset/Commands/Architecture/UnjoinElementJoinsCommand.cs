using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Architecture;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class UnjoinElementJoinsCommand : ExternalEventCommandBase
    {
        private UnjoinElementJoinsEventHandler _handler => (UnjoinElementJoinsEventHandler)Handler;
        public override string CommandName => "unjoin_element_joins";

        public UnjoinElementJoinsCommand(UIApplication uiApp) : base(new UnjoinElementJoinsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            string sourceCategory = parameters?["sourceCategory"]?.Value<string>();
            var targetCatsArray = parameters?["targetCategories"] as JArray;
            List<string> targetCategories = targetCatsArray != null ? targetCatsArray.Select(c => (string)c).ToList() : null;

            var elemIdsArray = parameters?["elementIds"] as JArray;
            List<int> elementIds = elemIdsArray != null ? elemIdsArray.Select(id => (int)id).ToList() : null;
            int? viewId = parameters?["viewId"]?.Value<int?>();

            if (string.IsNullOrEmpty(sourceCategory))
                throw new ArgumentException("sourceCategory is required");

            _handler.SetParameters(sourceCategory, targetCategories, elementIds, viewId);
            if (RaiseAndWaitForCompletion(30000)) return _handler.Result;
            throw new TimeoutException("unjoin_element_joins timed out");
        }
    }
}
