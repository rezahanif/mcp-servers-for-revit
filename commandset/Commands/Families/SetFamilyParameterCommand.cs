using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Commands.Families
{
    public class SetFamilyParameterCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private SetFamilyParameterEventHandler _handler => (SetFamilyParameterEventHandler)Handler;

        public override string CommandName => "set_family_parameter";

        public SetFamilyParameterCommand(UIApplication uiApp)
            : base(new SetFamilyParameterEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    _handler.ElementId = parameters?["elementId"]?.Value<long>();
                    _handler.FamilyName = parameters?["familyName"]?.ToString();
                    _handler.TypeName = parameters?["typeName"]?.ToString();

                    var paramArray = parameters?["parameters"] as JArray;
                    if (paramArray == null || paramArray.Count == 0)
                    {
                        throw new ArgumentException("parameters array is required");
                    }

                    var updates = new List<SetFamilyParameterEventHandler.ParamUpdate>();
                    foreach (var p in paramArray)
                    {
                        updates.Add(new SetFamilyParameterEventHandler.ParamUpdate
                        {
                            Name = p["name"]?.ToString() ?? throw new ArgumentException("parameter name is required"),
                            Value = p["value"]?.ToString() ?? throw new ArgumentException("parameter value is required"),
                            ParameterType = p["parameterType"]?.ToString()
                        });
                    }

                    _handler.Parameters = updates;

                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for set_family_parameter external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to set family parameter: {ex.Message}", ex);
                }
            }
        }
    }
}
