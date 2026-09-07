using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Families
{
    public class GetFamilyTypesCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private GetFamilyTypesEventHandler _handler => (GetFamilyTypesEventHandler)Handler;

        public override string CommandName => "get_family_types";

        public GetFamilyTypesCommand(UIApplication uiApp)
            : base(new GetFamilyTypesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    _handler.FamilyName = parameters?["familyName"]?.ToString();
                    _handler.CategoryName = parameters?["categoryName"]?.ToString();
                    _handler.IncludeParameters = parameters?["includeParameters"]?.Value<bool>() ?? false;

                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for get_family_types external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to get family types: {ex.Message}", ex);
                }
            }
        }
    }
}
