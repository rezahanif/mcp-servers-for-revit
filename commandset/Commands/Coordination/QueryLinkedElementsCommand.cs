using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Coordination;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Coordination
{
    public class QueryLinkedElementsCommand : ExternalEventCommandBase
    {
        private QueryLinkedElementsEventHandler _handler => (QueryLinkedElementsEventHandler)Handler;

        public override string CommandName => "query_linked_elements";

        public QueryLinkedElementsCommand(UIApplication uiApp)
            : base(new QueryLinkedElementsEventHandler(), uiApp)
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
                    throw new TimeoutException("Query linked elements operation timed out");
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
