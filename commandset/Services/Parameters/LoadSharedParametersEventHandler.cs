using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Parameters
{
    public class LoadSharedParametersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                string filePath = _parameters?["filePath"]?.Value<string>();
                JArray categoriesArr = _parameters?["categories"] as JArray;
                bool bindToInstance = _parameters?["bindToInstance"]?.Value<bool>() ?? false;
                JArray groupFilterArr = _parameters?["groupFilter"] as JArray;
                bool dryRun = _parameters?["dryRun"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(filePath))
                    throw new ArgumentException("Parameter 'filePath' is required");

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Shared parameter file not found: {filePath}");

                if (categoriesArr == null || categoriesArr.Count == 0)
                    throw new ArgumentException("Parameter 'categories' must contain at least one category");

                var targetCategories = new CategorySet();
                var builtInMap = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Walls", BuiltInCategory.OST_Walls },
                    { "Floors", BuiltInCategory.OST_Floors },
                    { "Ceilings", BuiltInCategory.OST_Ceilings },
                    { "Windows", BuiltInCategory.OST_Windows },
                    { "Doors", BuiltInCategory.OST_Doors },
                    { "Materials", BuiltInCategory.OST_Materials },
                    { "Roofs", BuiltInCategory.OST_Roofs },
                    { "CurtainPanels", BuiltInCategory.OST_CurtainWallPanels },
                    { "StructuralFraming", BuiltInCategory.OST_StructuralFraming },
                };

                foreach (var catName in categoriesArr)
                {
                    string name = catName.Value<string>();

                    if (string.Equals(name, "Columns", StringComparison.OrdinalIgnoreCase))
                    {
                        Category archColCat = Category.GetCategory(doc, BuiltInCategory.OST_Columns);
                        Category structColCat = Category.GetCategory(doc, BuiltInCategory.OST_StructuralColumns);
                        if (archColCat == null && structColCat == null)
                            throw new InvalidOperationException($"Cannot identify category: {name}");
                        if (archColCat != null) targetCategories.Insert(archColCat);
                        if (structColCat != null) targetCategories.Insert(structColCat);
                        continue;
                    }

                    Category cat = null;
                    if (builtInMap.TryGetValue(name, out var bic))
                    {
                        cat = Category.GetCategory(doc, bic);
                    }

                    if (cat == null)
                        throw new InvalidOperationException($"Cannot identify category: {name}");

                    targetCategories.Insert(cat);
                }

                HashSet<int> groupFilter = null;
                if (groupFilterArr != null && groupFilterArr.Count > 0)
                {
                    groupFilter = new HashSet<int>(groupFilterArr.Select(g => g.Value<int>()));
                }

                var app = doc.Application;
                string originalFile = app.SharedParametersFilename;

                try
                {
                    app.SharedParametersFilename = filePath;
                    DefinitionFile defFile = app.OpenSharedParameterFile();
                    if (defFile == null)
                        throw new InvalidOperationException($"Cannot open shared parameter file: {filePath}");

                    if (dryRun)
                    {
                        int wouldBind = 0, wouldExpand = 0, wouldSkip = 0, wouldRebind = 0;
                        var planItems = new List<object>();

                        foreach (DefinitionGroup defGroup in defFile.Groups)
                        {
                            foreach (Definition def in defGroup.Definitions)
                            {
                                ExternalDefinition exDef = def as ExternalDefinition;
                                if (exDef == null) continue;

                                BindingMap bindingMap = doc.ParameterBindings;
                                Definition existingDef = null;
                                var it = bindingMap.ForwardIterator();
                                while (it.MoveNext())
                                {
                                    if (it.Key.Name == exDef.Name) { existingDef = it.Key; break; }
                                }

                                if (existingDef == null)
                                {
                                    wouldBind++;
                                    planItems.Add(new
                                    {
                                        ParameterName = exDef.Name,
                                        Group = defGroup.Name,
                                        DataType = exDef.GetDataType().TypeId,
                                        Status = "Not bound yet; will create new binding"
                                    });
                                    continue;
                                }

                                Binding existingBinding = bindingMap.get_Item(existingDef);
                                bool isInstanceBinding = existingBinding is InstanceBinding;
                                bool bindingKindMismatch = (!bindToInstance && isInstanceBinding) || (bindToInstance && !isInstanceBinding);

                                if (bindingKindMismatch)
                                {
                                    wouldRebind++;
                                    planItems.Add(new
                                    {
                                        ParameterName = exDef.Name,
                                        Group = defGroup.Name,
                                        Status = "Binding type mismatch; will rebind"
                                    });
                                    continue;
                                }

                                ElementBinding elemBinding = existingBinding as ElementBinding;
                                CategorySet existingCats = elemBinding?.Categories;
                                bool needsExpand = false;
                                if (existingCats != null)
                                {
                                    foreach (Category targetCat in targetCategories)
                                    {
                                        bool covered = false;
                                        foreach (Category existingCat in existingCats)
                                        {
                                            if (existingCat.Id == targetCat.Id) { covered = true; break; }
                                        }
                                        if (!covered) { needsExpand = true; break; }
                                    }
                                }

                                if (needsExpand)
                                {
                                    wouldExpand++;
                                    planItems.Add(new
                                    {
                                        ParameterName = exDef.Name,
                                        Group = defGroup.Name,
                                        Status = "Missing categories; will expand binding"
                                    });
                                }
                                else
                                {
                                    wouldSkip++;
                                    planItems.Add(new
                                    {
                                        ParameterName = exDef.Name,
                                        Group = defGroup.Name,
                                        Status = "Matching binding exists; will skip"
                                    });
                                }
                            }
                        }

                        Result = new AIResult<object>
                        {
                            Success = true,
                            Message = $"[dryRun] Planned new bindings: {wouldBind}, rebindings: {wouldRebind}, expansions: {wouldExpand}, skips: {wouldSkip}",
                            Response = new
                            {
                                DryRun = true,
                                FilePath = filePath,
                                WouldBindCount = wouldBind,
                                WouldRebindCount = wouldRebind,
                                WouldExpandCount = wouldExpand,
                                WouldSkipCount = wouldSkip,
                                Categories = categoriesArr.Select(c => c.Value<string>()).ToArray(),
                                BindingLevel = bindToInstance ? "Instance" : "Type",
                                Parameters = planItems
                            }
                        };
                        return;
                    }

                    int totalBound = 0;
                    int totalSkipped = 0;
                    int totalFailed = 0;
                    var results = new List<object>();

                    using (Transaction trans = new Transaction(doc, "Load Shared Parameters"))
                    {
                        trans.Start();

                        foreach (DefinitionGroup defGroup in defFile.Groups)
                        {
                            foreach (Definition def in defGroup.Definitions)
                            {
                                ExternalDefinition exDef = def as ExternalDefinition;
                                if (exDef == null) continue;

                                BindingMap bindingMap = doc.ParameterBindings;
                                Definition existingDef = null;

                                var it = bindingMap.ForwardIterator();
                                while (it.MoveNext())
                                {
                                    if (it.Key.Name == exDef.Name)
                                    {
                                        existingDef = it.Key;
                                        break;
                                    }
                                }

                                if (existingDef != null)
                                {
                                    Binding existingBinding = bindingMap.get_Item(existingDef);
                                    bool isInstanceBinding = existingBinding is InstanceBinding;

                                    if (!bindToInstance && isInstanceBinding)
                                    {
                                        bindingMap.Remove(existingDef);
                                    }
                                    else if (bindToInstance && !isInstanceBinding)
                                    {
                                        bindingMap.Remove(existingDef);
                                    }
                                    else
                                    {
                                        ElementBinding elemBinding = existingBinding as ElementBinding;
                                        CategorySet existingCats = elemBinding?.Categories;
                                        bool needsExpand = false;

                                        if (existingCats != null)
                                        {
                                            foreach (Category targetCat in targetCategories)
                                            {
                                                bool alreadyCovered = false;
                                                foreach (Category existingCat in existingCats)
                                                {
                                                    if (existingCat.Id == targetCat.Id)
                                                    {
                                                        alreadyCovered = true;
                                                        break;
                                                    }
                                                }
                                                if (!alreadyCovered)
                                                {
                                                    existingCats.Insert(targetCat);
                                                    needsExpand = true;
                                                }
                                            }
                                        }

                                        if (needsExpand)
                                        {
                                            bool reinserted;
                                            try { reinserted = bindingMap.ReInsert(existingDef, elemBinding); }
                                            catch { reinserted = false; }

                                            if (reinserted)
                                            {
                                                totalBound++;
                                                results.Add(new
                                                {
                                                    ParameterName = exDef.Name,
                                                    Group = defGroup.Name,
                                                    Status = "Expanded binding to new categories"
                                                });
                                            }
                                            else
                                            {
                                                totalFailed++;
                                                results.Add(new
                                                {
                                                    ParameterName = exDef.Name,
                                                    Group = defGroup.Name,
                                                    Status = "Expansion failed"
                                                });
                                            }
                                        }
                                        else
                                        {
                                            totalSkipped++;
                                            results.Add(new
                                            {
                                                ParameterName = exDef.Name,
                                                Group = defGroup.Name,
                                                Status = "Already bound, skipped"
                                            });
                                        }
                                        continue;
                                    }
                                }

                                ElementBinding binding;
                                if (bindToInstance)
                                {
                                    binding = app.Create.NewInstanceBinding(targetCategories);
                                }
                                else
                                {
                                    binding = app.Create.NewTypeBinding(targetCategories);
                                }

                                bool bound = false;
#if REVIT2024_OR_GREATER
                                try
                                {
                                    bound = bindingMap.Insert(exDef, binding, GroupTypeId.IdentityData);
                                }
                                catch {}
#else
                                try
                                {
                                    bound = bindingMap.Insert(exDef, binding, BuiltInParameterGroup.PG_IDENTITY_DATA);
                                }
                                catch {}
#endif

                                if (bound)
                                {
                                    totalBound++;
                                    results.Add(new
                                    {
                                        ParameterName = exDef.Name,
                                        Group = defGroup.Name,
                                        DataType = exDef.GetDataType().TypeId,
                                        Status = "Bound successfully"
                                    });
                                }
                                else
                                {
                                    totalFailed++;
                                    results.Add(new
                                    {
                                        ParameterName = exDef.Name,
                                        Group = defGroup.Name,
                                        Status = "Binding failed"
                                    });
                                }
                            }
                        }

                        trans.Commit();
                    }

                    Result = new AIResult<object>
                    {
                        Success = totalFailed == 0,
                        Message = $"Successfully bound {totalBound} shared parameters to {targetCategories.Size} categories",
                        Response = new
                        {
                            FilePath = filePath,
                            TotalBound = totalBound,
                            TotalSkipped = totalSkipped,
                            TotalFailed = totalFailed,
                            Categories = categoriesArr.Select(c => c.Value<string>()).ToArray(),
                            BindingLevel = bindToInstance ? "Instance" : "Type",
                            Parameters = results
                        }
                    };
                }
                finally
                {
                    app.SharedParametersFilename = originalFile ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error loading shared parameters: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "LoadSharedParametersEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 25000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
