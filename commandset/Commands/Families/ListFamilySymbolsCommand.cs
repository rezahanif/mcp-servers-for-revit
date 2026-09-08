using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Families;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Families
{
    public class ListFamilySymbolsCommand : ExternalEventCommandBase
    {
        private ListFamilySymbolsEventHandler _handler => (ListFamilySymbolsEventHandler)Handler;

        public override string CommandName => "list_family_symbols";

        public ListFamilySymbolsCommand(UIApplication uiApp)
            : base(new ListFamilySymbolsEventHandler(), uiApp)
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
                    throw new TimeoutException("List family symbols operation timed out");
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
