using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Architecture;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class QueryWallsByLocationCommand : ExternalEventCommandBase
    {
        private QueryWallsByLocationEventHandler _handler => (QueryWallsByLocationEventHandler)Handler;

        public override string CommandName => "query_walls_by_location";

        public QueryWallsByLocationCommand(UIApplication uiApp)
            : base(new QueryWallsByLocationEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                double x = parameters["x"]?.ToObject<double>() ?? 0;
                double y = parameters["y"]?.ToObject<double>() ?? 0;
                double searchRadius = parameters["searchRadius"]?.ToObject<double>() ?? 5000;
                string level = parameters["level"]?.ToObject<string>();

                _handler.SetParameters(x, y, searchRadius, level);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Query walls by location operation timed out");
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
    }
}
