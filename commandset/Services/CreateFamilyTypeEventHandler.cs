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
    public class CreateFamilyTypeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string FamilyName { get; set; }
        public string NewTypeName { get; set; }
        public string DuplicateFrom { get; set; }
        public Dictionary<string, string> Parameters { get; set; }

        public AIResult<object> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Create Family Type";

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

                var baseSymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(fs =>
                        fs.FamilyName.Equals(FamilyName, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrEmpty(DuplicateFrom) || fs.Name.Equals(DuplicateFrom, StringComparison.OrdinalIgnoreCase)));

                if (baseSymbol == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = $"Could not find family '{FamilyName}' to duplicate from."
                    };
                    return;
                }

                FamilySymbol newSymbol = null;
                var warnings = new List<string>();

                TransactionUtils.ExecuteInTransaction(doc, $"Create Family Type {NewTypeName}", () =>
                {
                    newSymbol = baseSymbol.Duplicate(NewTypeName) as FamilySymbol;
                    if (newSymbol != null && Parameters != null)
                    {
                        foreach (var kvp in Parameters)
                        {
                            var p = newSymbol.LookupParameter(kvp.Key);
                            if (p != null && !p.IsReadOnly)
                            {
                                if (p.StorageType == StorageType.Double && double.TryParse(kvp.Value, out var dVal))
                                {
                                    p.Set(dVal / 304.8); // convert mm to feet
                                }
                                else if (p.StorageType == StorageType.Integer && int.TryParse(kvp.Value, out var iVal))
                                {
                                    p.Set(iVal);
                                }
                                else
                                {
                                    p.Set(kvp.Value);
                                }
                            }
                            else
                            {
                                warnings.Add($"Parameter '{kvp.Key}' could not be set on new type (not found or read-only).");
                            }
                        }
                    }
                });

                if (newSymbol != null)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"Created family type '{NewTypeName}' in family '{FamilyName}' (ID: {newSymbol.Id.GetValue()}).",
                        Response = new
                        {
                            TypeId = newSymbol.Id.GetValue(),
                            TypeName = newSymbol.Name,
                            FamilyName = newSymbol.FamilyName
                        },
                        Warnings = warnings
                    };
                }
                else
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = "Failed to create family type."
                    };
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error creating family type: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
