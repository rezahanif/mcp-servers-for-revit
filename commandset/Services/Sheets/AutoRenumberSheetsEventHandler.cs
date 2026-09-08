using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Sheets
{
    public class AutoRenumberSheetsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<object> Result { get; private set; }

        public void SetParameters()
        {
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                // Phase 0: Emergency Recovery (Fix _MCPFIX)
                var fixSheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => s.SheetNumber.EndsWith("_MCPFIX"))
                    .ToList();

                if (fixSheets.Count > 0)
                {
                    using (Transaction tFix = new Transaction(doc, "Restore _MCPFIX Sheets"))
                    {
                        tFix.Start();
                        foreach (var s in fixSheets)
                        {
                            string original = s.SheetNumber.Replace("_MCPFIX", "");
                            try { s.SheetNumber = original; }
                            catch { }
                        }
                        tFix.Commit();
                    }
                }

                // 1. Scan all sheets
                var allSheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                var insertSheets = allSheets
                    .Where(s => s.SheetNumber.EndsWith("-1"))
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                if (insertSheets.Count == 0)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = "No sheets with '-1' suffix found in the project",
                        Response = new { Success = true, Message = "No sheets with '-1' suffix found", ChangedCount = 0 }
                    };
                    return;
                }

                var sheetMap = allSheets.ToDictionary(s => s.SheetNumber, s => s.Id.GetIntValue());
                var finalMoves = new Dictionary<int, string>();
                var reservationMap = new Dictionary<string, int>(sheetMap.Count);
                foreach (var kvp in sheetMap) reservationMap[kvp.Key] = kvp.Value;

                int processedInsertions = 0;

                foreach (var sourceSheet in insertSheets)
                {
                    int sourceId = sourceSheet.Id.GetIntValue();
                    string sourceNumber = sourceSheet.SheetNumber;
                    string baseNumber = sourceNumber.Substring(0, sourceNumber.Length - 2);
                    string targetNumber = IncrementString(baseNumber);

                    string currentMoverNumber = targetNumber;
                    int currentMoverId = sourceId;

                    while (true)
                    {
                        if (reservationMap.ContainsKey(currentMoverNumber))
                        {
                            int occupierId = reservationMap[currentMoverNumber];
                            if (occupierId == currentMoverId) break;

                            finalMoves[currentMoverId] = currentMoverNumber;
                            reservationMap[currentMoverNumber] = currentMoverId;
                            currentMoverId = occupierId;
                            currentMoverNumber = IncrementString(currentMoverNumber);
                        }
                        else
                        {
                            finalMoves[currentMoverId] = currentMoverNumber;
                            reservationMap[currentMoverNumber] = currentMoverId;
                            break;
                        }

                        if (finalMoves.Count > 2000) break;
                    }

                    processedInsertions++;
                }

                int changedCount = 0;
                if (finalMoves.Count > 0)
                {
                    using (TransactionGroup tg = new TransactionGroup(doc, "Auto Renumber Sheets"))
                    {
                        tg.Start();

                        using (Transaction t1 = new Transaction(doc, "Step 1: Temp Names"))
                        {
                            t1.Start();
                            foreach (var id in finalMoves.Keys)
                            {
                                Element elem = doc.GetElement(new ElementId(id));
                                if (elem != null)
                                {
                                    Parameter p = elem.get_Parameter(BuiltInParameter.SHEET_NUMBER);
                                    if (p != null) p.Set(p.AsString() + "_TEMP_" + Guid.NewGuid().ToString().Substring(0, 5));
                                }
                            }
                            t1.Commit();
                        }

                        using (Transaction t2 = new Transaction(doc, "Step 2: Final Renumber"))
                        {
                            t2.Start();
                            foreach (var kvp in finalMoves)
                            {
                                Element elem = doc.GetElement(new ElementId(kvp.Key));
                                if (elem != null)
                                {
                                    Parameter p = elem.get_Parameter(BuiltInParameter.SHEET_NUMBER);
                                    if (p != null)
                                    {
                                        p.Set(kvp.Value);
                                        changedCount++;
                                    }
                                }
                            }
                            t2.Commit();
                        }

                        tg.Assimilate();
                    }
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Renumber completed: processed {processedInsertions} insertion sheet(s), updated {changedCount} sheet number(s)",
                    Response = new
                    {
                        Success = true,
                        ChangedCount = changedCount,
                        InsertionsResolved = processedInsertions,
                        Message = $"Renumber completed: processed {processedInsertions} insertion sheet(s), updated {changedCount} sheet number(s)"
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error renumbering sheets: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private static string IncrementString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "1";
            var match = Regex.Match(input, @"^(.*?)(d+)$");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                string numStr = match.Groups[2].Value;
                if (long.TryParse(numStr, out long num))
                {
                    long nextNum = num + 1;
                    return prefix + nextNum.ToString().PadLeft(numStr.Length, '0');
                }
            }
            return input + "-1";
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "AutoRenumberSheetsEventHandler";
    }
}
