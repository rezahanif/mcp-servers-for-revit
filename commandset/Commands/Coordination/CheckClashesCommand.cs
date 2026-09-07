using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Linq;

namespace RevitMCPCommandSet.Commands.Coordination
{
    /// <summary>
    /// check_clashes — perform multi-type geometric interference and clash checks between Revit elements.
    /// </summary>
    public class CheckClashesCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private CheckClashesEventHandler _handler => (CheckClashesEventHandler)Handler;

        public override string CommandName => "check_clashes";

        public CheckClashesCommand(UIApplication uiApp)
            : base(new CheckClashesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    _handler.CategoryA = parameters?["categoryA"]?.ToString();
                    _handler.CategoryB = parameters?["categoryB"]?.ToString();

                    var idsA = parameters?["elementIdsA"] as JArray;
                    if (idsA != null)
                    {
                        _handler.ElementIdsA = idsA.Select(x => x.Value<long>()).ToList();
                    }
                    else
                    {
                        _handler.ElementIdsA = null;
                    }

                    var idsB = parameters?["elementIdsB"] as JArray;
                    if (idsB != null)
                    {
                        _handler.ElementIdsB = idsB.Select(x => x.Value<long>()).ToList();
                    }
                    else
                    {
                        _handler.ElementIdsB = null;
                    }

                    _handler.Limit = parameters?["limit"]?.Value<int>() ?? 50;

                    if (RaiseAndWaitForCompletion(25000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for check_clashes external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to check clashes: {ex.Message}", ex);
                }
            }
        }
    }
}
