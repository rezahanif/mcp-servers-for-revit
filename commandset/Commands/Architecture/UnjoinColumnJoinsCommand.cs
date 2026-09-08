using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Architecture;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class UnjoinColumnJoinsCommand : ExternalEventCommandBase
    {
        private UnjoinColumnJoinsEventHandler _handler => (UnjoinColumnJoinsEventHandler)Handler;
        public override string CommandName => "unjoin_column_joins";

        public UnjoinColumnJoinsCommand(UIApplication uiApp) : base(new UnjoinColumnJoinsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            var colsArray = parameters?["columnIds"] as JArray;
            List<int> columnIds = colsArray != null ? colsArray.Select(id => (int)id).ToList() : null;
            int? viewId = parameters?["viewId"]?.Value<int?>();

            _handler.SetParameters(columnIds, viewId);
            if (RaiseAndWaitForCompletion(30000)) return _handler.Result;
            throw new TimeoutException("unjoin_column_joins timed out");
        }
    }
}
