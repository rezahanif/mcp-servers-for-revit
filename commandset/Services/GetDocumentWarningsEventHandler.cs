using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class GetDocumentWarningsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<List<DocumentWarningInfo>> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Get Document Warnings";

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<List<DocumentWarningInfo>>
                    {
                        Success = false,
                        Message = "No active Revit document found.",
                        Response = new List<DocumentWarningInfo>()
                    };
                    return;
                }

                IList<FailureMessage> warnings = doc.GetWarnings();
                var list = new List<DocumentWarningInfo>();

                foreach (var w in warnings)
                {
                    var info = new DocumentWarningInfo
                    {
                        Description = w.GetDescriptionText(),
                        Severity = w.GetSeverity().ToString(),
                        ElementIds = w.GetFailingElements().Select(id => id.GetValue()).ToList()
                    };

                    foreach (var id in w.GetFailingElements())
                    {
                        var elem = doc.GetElement(id);
                        if (elem != null)
                        {
                            info.ElementSummaries.Add($"{elem.Category?.Name ?? "Element"}: '{elem.Name}' (ID: {id.GetValue()})");
                        }
                    }

                    list.Add(info);
                }

                Result = new AIResult<List<DocumentWarningInfo>>
                {
                    Success = true,
                    Message = $"Retrieved {list.Count} document warning(s).",
                    Response = list
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<DocumentWarningInfo>>
                {
                    Success = false,
                    Message = $"Failed to get document warnings: {ex.Message}",
                    Response = new List<DocumentWarningInfo>()
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
