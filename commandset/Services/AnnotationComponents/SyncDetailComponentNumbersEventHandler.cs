using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.AnnotationComponents
{
    public class SyncDetailComponentNumbersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private JObject _parameters;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(JObject parameters)
        {
            _parameters = parameters;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                int updatedInstances = 0;
                int typesCreated = 0;
                var processedTypes = new HashSet<string>();

                var viewToSheetMap = new Dictionary<long, ViewSheet>();
                var sheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                foreach (var sheet in sheets)
                {
                    viewToSheetMap[sheet.Id.GetIntValue()] = sheet;
                    foreach (var vpId in sheet.GetAllViewports())
                    {
                        var vp = doc.GetElement(vpId) as Viewport;
                        if (vp != null)
                            viewToSheetMap[vp.ViewId.GetIntValue()] = sheet;
                    }
                }

                var categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_DetailComponents,
                    BuiltInCategory.OST_GenericAnnotation
                };

                var allInstances = new List<FamilyInstance>();
                foreach (var cat in categories)
                {
                    var instances = new FilteredElementCollector(doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .Where(e =>
                        {
                            var fi = e as FamilyInstance;
                            return fi != null && fi.Symbol != null && fi.Symbol.FamilyName != null && fi.Symbol.FamilyName.Contains("AE-圖號");
                        })
                        .Cast<FamilyInstance>();
                    allInstances.AddRange(instances);
                }

                using (TransactionGroup tg = new TransactionGroup(doc, "Sync Detail Component Numbers"))
                {
                    tg.Start();

                    foreach (var instance in allInstances)
                    {
                        long ownerViewId = instance.OwnerViewId.GetIntValue();
                        ViewSheet sheet = null;

                        if (!viewToSheetMap.TryGetValue(ownerViewId, out sheet))
                        {
                            Element ownerView = doc.GetElement(ownerViewId.ToElementId());
                            sheet = ownerView as ViewSheet;
                            if (sheet == null) continue;
                        }

                        string sheetNumber = sheet.SheetNumber;
                        string sheetName = sheet.Name;
                        FamilySymbol currentSymbol = instance.Symbol;

                        if (!currentSymbol.Name.StartsWith(sheetNumber + "-"))
                            continue;

                        string typeKey = $"{currentSymbol.FamilyName}:{currentSymbol.Name}";
                        if (processedTypes.Contains(typeKey)) continue;

                        using (Transaction t = new Transaction(doc, "Sync Component Type"))
                        {
                            t.Start();

                            Parameter pTypeSheetNum = currentSymbol.LookupParameter("詳圖圖號");
                            if (pTypeSheetNum != null && !pTypeSheetNum.IsReadOnly)
                                pTypeSheetNum.Set(sheetNumber);

                            Parameter pTypeSheetName = currentSymbol.LookupParameter("圖說名稱");
                            if (pTypeSheetName != null && !pTypeSheetName.IsReadOnly)
                                pTypeSheetName.Set(sheetName);

                            t.Commit();
                            updatedInstances++;
                            processedTypes.Add(typeKey);
                        }
                    }
                    tg.Assimilate();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Sync completed: updated {updatedInstances} instances.",
                    Response = new
                    {
                        UpdatedInstances = updatedInstances,
                        TypesCreated = typesCreated
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error syncing detail component numbers: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "SyncDetailComponentNumbersEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 20000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
