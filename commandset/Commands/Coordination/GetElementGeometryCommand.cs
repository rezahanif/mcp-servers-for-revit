using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Coordination;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Coordination
{
    public class GetElementGeometryCommand : ExternalEventCommandBase
    {
        private GetElementGeometryEventHandler _handler => (GetElementGeometryEventHandler)Handler;

        public override string CommandName => "get_element_geometry";

        public GetElementGeometryCommand(UIApplication uiApp)
            : base(new GetElementGeometryEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters(parameters);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get element geometry operation timed out");
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
