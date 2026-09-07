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
    public class CheckClashesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string CategoryA { get; set; }
        public string CategoryB { get; set; }
        public List<long> ElementIdsA { get; set; }
        public List<long> ElementIdsB { get; set; }
        public int Limit { get; set; } = 50;

        public AIResult<List<ClashResult>> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Check Clashes";

        private BuiltInCategory ResolveCategory(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return BuiltInCategory.INVALID;
            if (Enum.TryParse<BuiltInCategory>(name, true, out var cat)) return cat;
            if (!name.StartsWith("OST_") && Enum.TryParse<BuiltInCategory>("OST_" + name, true, out cat)) return cat;
            return BuiltInCategory.INVALID;
        }

        private List<Element> GetElements(Document doc, string catName, List<long> ids)
        {
            if (ids != null && ids.Count > 0)
            {
                var list = new List<Element>();
                foreach (var id in ids)
                {
#if REVIT2024_OR_GREATER
                    var el = doc.GetElement(new ElementId(id));
#else
                    var el = doc.GetElement(new ElementId((int)id));
#endif
                    if (el != null) list.Add(el);
                }
                return list;
            }

            var builtInCat = ResolveCategory(catName);
            if (builtInCat == BuiltInCategory.INVALID)
            {
                builtInCat = BuiltInCategory.OST_Walls; // default to walls
            }

            return new FilteredElementCollector(doc)
                .OfCategory(builtInCat)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<List<ClashResult>>
                    {
                        Success = false,
                        Message = "No active Revit document.",
                        Response = new List<ClashResult>()
                    };
                    return;
                }

                var setA = GetElements(doc, CategoryA, ElementIdsA);
                bool selfCheck = string.IsNullOrWhiteSpace(CategoryB) && (ElementIdsB == null || ElementIdsB.Count == 0);
                var setB = selfCheck ? setA : GetElements(doc, CategoryB, ElementIdsB);

                var clashes = new List<ClashResult>();
                var seenPairs = new HashSet<string>();

                foreach (var elemA in setA)
                {
                    if (clashes.Count >= Limit) break;
                    if (elemA.get_BoundingBox(null) == null) continue;

                    // Use Revit's native ElementIntersectsElementFilter
                    var filter = new ElementIntersectsElementFilter(elemA);
                    var collector = new FilteredElementCollector(doc)
                        .WherePasses(filter)
                        .WhereElementIsNotElementType();

                    var intersectingElements = collector.ToElements();
                    foreach (var elemB in intersectingElements)
                    {
                        if (elemA.Id == elemB.Id) continue;
                        if (!setB.Any(b => b.Id == elemB.Id)) continue;

                        long idA = elemA.Id.GetValue();
                        long idB = elemB.Id.GetValue();
                        string pairKey = idA < idB ? $"{idA}-{idB}" : $"{idB}-{idA}";
                        if (seenPairs.Contains(pairKey)) continue;
                        seenPairs.Add(pairKey);

                        string clashType = "HardClash";
                        string rec = "Use resolve_geometry_clash with action 'cut' or 'join' to resolve the overlapping volume.";

                        if (elemA is Wall && elemB is Wall)
                        {
                            clashType = "WallOverlap";
                            rec = "Use Cut Geometry (resolve_geometry_clash with action 'cut') to embed one wall within the other, or adjust location lines.";
                        }

                        clashes.Add(new ClashResult
                        {
                            ElementIdA = idA,
                            NameA = elemA.Name,
                            CategoryA = elemA.Category?.Name ?? "Unknown",
                            ElementIdB = idB,
                            NameB = elemB.Name,
                            CategoryB = elemB.Category?.Name ?? "Unknown",
                            ClashType = clashType,
                            Recommendation = rec
                        });

                        if (clashes.Count >= Limit) break;
                    }
                }

                Result = new AIResult<List<ClashResult>>
                {
                    Success = true,
                    Message = $"Detected {clashes.Count} clash(es) between target element sets.",
                    Response = clashes
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<ClashResult>>
                {
                    Success = false,
                    Message = $"Error during clash check: {ex.Message}",
                    Response = new List<ClashResult>()
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
