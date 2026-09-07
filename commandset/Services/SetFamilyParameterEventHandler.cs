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
    public class SetFamilyParameterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public long? ElementId { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }

        public class ParamUpdate
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string ParameterType { get; set; }
        }

        public List<ParamUpdate> Parameters { get; set; } = new List<ParamUpdate>();
        public AIResult<object> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Set Family Parameter";

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<object> { Success = false, Message = "No active Revit document." };
                    return;
                }

                Element targetElement = null;

                if (ElementId.HasValue)
                {
#if REVIT2024_OR_GREATER
                    targetElement = doc.GetElement(new ElementId(ElementId.Value));
#else
                    targetElement = doc.GetElement(new ElementId((int)ElementId.Value));
#endif
                }
                else if (!string.IsNullOrEmpty(FamilyName) && !string.IsNullOrEmpty(TypeName))
                {
                    targetElement = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(fs => fs.FamilyName.Equals(FamilyName, StringComparison.OrdinalIgnoreCase) &&
                                              fs.Name.Equals(TypeName, StringComparison.OrdinalIgnoreCase));
                }

                if (targetElement == null)
                {
                    Result = new AIResult<object> { Success = false, Message = "Target element or family type could not be found." };
                    return;
                }

                int successCount = 0;
                var warnings = new List<string>();

                TransactionUtils.ExecuteInTransaction(doc, "Set Family Parameter", () =>
                {
                    foreach (var update in Parameters)
                    {
                        var p = targetElement.LookupParameter(update.Name);
                        if (p == null || p.IsReadOnly)
                        {
                            warnings.Add($"Parameter '{update.Name}' not found or is read-only on target element.");
                            continue;
                        }

                        bool set = false;
                        switch (p.StorageType)
                        {
                            case StorageType.String:
                                set = p.Set(update.Value);
                                break;
                            case StorageType.Integer:
                                if (int.TryParse(update.Value, out var iVal))
                                    set = p.Set(iVal);
                                break;
                            case StorageType.Double:
                                if (double.TryParse(update.Value, out var dVal))
                                {
                                    // If length parameter, assume input in mm and convert to internal feet
                                    if (p.Definition.GetDataType() == SpecTypeId.Length)
                                    {
                                        dVal = dVal / 304.8;
                                    }
                                    set = p.Set(dVal);
                                }
                                break;
                            case StorageType.ElementId:
                                if (long.TryParse(update.Value, out var idVal))
                                {
#if REVIT2024_OR_GREATER
                                    set = p.Set(new ElementId(idVal));
#else
                                    set = p.Set(new ElementId((int)idVal));
#endif
                                }
                                break;
                        }

                        if (set) successCount++;
                        else warnings.Add($"Failed to set value on parameter '{update.Name}'.");
                    }
                });

                Result = new AIResult<object>
                {
                    Success = successCount > 0,
                    Message = $"Updated {successCount} of {Parameters.Count} parameter(s) on element {targetElement.Id.GetValue()}.",
                    Response = new { Updated = successCount, TargetId = targetElement.Id.GetValue() },
                    Warnings = warnings
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error setting family parameter: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
