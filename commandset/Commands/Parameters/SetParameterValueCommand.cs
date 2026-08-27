using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Commands.Parameters
{
    /// <summary>
    /// set_parameter_value — write parameter values onto existing elements.
    ///
    /// Closes the gap the API benchmark called the connector's #1 structural
    /// problem: it could author a model it could not then revise.
    /// </summary>
    public class SetParameterValueCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private SetParameterValueEventHandler _handler => (SetParameterValueEventHandler)Handler;

        public override string CommandName => "set_parameter_value";

        public SetParameterValueCommand(UIApplication uiApp)
            : base(new SetParameterValueEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    var data = parameters?["data"] as JArray;
                    if (data == null || data.Count == 0)
                    {
                        throw new ArgumentException("data must be a non-empty array of {elementId, parameters}");
                    }

                    var updates = new List<SetParameterValueEventHandler.ElementUpdate>();
                    foreach (var item in data)
                    {
                        var elementId = item["elementId"]?.Value<long>()
                            ?? throw new ArgumentException("each entry needs an elementId");

                        var paramArray = item["parameters"] as JArray;
                        if (paramArray == null || paramArray.Count == 0)
                        {
                            throw new ArgumentException($"element {elementId} has no parameters to set");
                        }

                        var updateList = paramArray.Select(p => new SetParameterValueEventHandler.ParameterUpdate
                        {
                            Name = p["name"]?.ToString(),
                            // Accepted as a string on purpose: the MCP schema declares
                            // string values and Revit's own storage type decides how to
                            // parse them. Guessing a JSON type here would lose "3.5" vs 3.5.
                            Value = p["value"]?.ToString(),
                            Units = p["units"]?.ToString(),
                        }).ToList();

                        updates.Add(new SetParameterValueEventHandler.ElementUpdate
                        {
                            ElementId = elementId,
                            Parameters = updateList,
                            IsTypeParameter = item["isTypeParameter"]?.Value<bool>() ?? false,
                        });
                    }

                    _handler.Updates = updates;

                    // Scaled to the batch: a large revision across many elements
                    // legitimately takes longer than a single write, and timing out
                    // mid-transaction is the one outcome worth avoiding.
                    int timeoutMs = Math.Min(120000, 15000 + updates.Count * 1000);
                    if (!RaiseAndWaitForCompletion(timeoutMs))
                    {
                        throw new TimeoutException($"set_parameter_value timed out after {timeoutMs} ms");
                    }

                    if (!_handler.IsSuccess)
                    {
                        var reason = _handler.Failures.FirstOrDefault()?.Reason ?? "unknown error";
                        throw new Exception(reason);
                    }

                    // Partial success is reported, not thrown: read-only and
                    // type-driven parameters legitimately refuse writes, and the
                    // caller needs to see WHICH ones did.
                    return new
                    {
                        updated = _handler.UpdatedCount,
                        failures = _handler.Failures.Select(f => new
                        {
                            elementId = f.ElementId,
                            parameter = f.Parameter,
                            reason = f.Reason,
                        }).ToList(),
                    };
                }
                catch (Exception ex)
                {
                    throw new Exception($"set_parameter_value failed: {ex.Message}");
                }
            }
        }
    }
}
