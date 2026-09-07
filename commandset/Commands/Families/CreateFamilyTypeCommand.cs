using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Commands.Families
{
    public class CreateFamilyTypeCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private CreateFamilyTypeEventHandler _handler => (CreateFamilyTypeEventHandler)Handler;

        public override string CommandName => "create_family_type";

        public CreateFamilyTypeCommand(UIApplication uiApp)
            : base(new CreateFamilyTypeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    _handler.FamilyName = parameters?["familyName"]?.ToString()
                        ?? throw new ArgumentException("familyName is required");
                    _handler.NewTypeName = parameters?["newTypeName"]?.ToString()
                        ?? throw new ArgumentException("newTypeName is required");
                    _handler.DuplicateFrom = parameters?["duplicateFrom"]?.ToString();

                    var paramObj = parameters?["parameters"] as JObject;
                    if (paramObj != null)
                    {
                        var dict = new Dictionary<string, string>();
                        foreach (var prop in paramObj.Properties())
                        {
                            dict[prop.Name] = prop.Value.ToString();
                        }
                        _handler.Parameters = dict;
                    }
                    else
                    {
                        _handler.Parameters = null;
                    }

                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for create_family_type external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to create family type: {ex.Message}", ex);
                }
            }
        }
    }
}
