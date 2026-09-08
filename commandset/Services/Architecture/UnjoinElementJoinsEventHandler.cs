using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    public class UnjoinElementJoinsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private string _sourceCategory;
        private List<string> _targetCategories;
        private List<int> _elementIds;
        private int? _viewId;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(string sourceCategory, List<string> targetCategories, List<int> elementIds, int? viewId)
        {
            _sourceCategory = sourceCategory;
            _targetCategories = targetCategories;
            _elementIds = elementIds;
            _viewId = viewId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                if (!Enum.TryParse($"OST_{_sourceCategory}", out BuiltInCategory sourceCat))
                    throw new Exception($"Could not parse sourceCategory: {_sourceCategory}");

                var targetCats = new List<BuiltInCategory>();
                var targetNames = new List<string>();

                if (_targetCategories != null && _targetCategories.Count > 0)
                {
                    foreach (var tc in _targetCategories)
                    {
                        if (Enum.TryParse($"OST_{tc}", out BuiltInCategory cat))
                        {
                            targetCats.Add(cat);
                            targetNames.Add(tc);
                        }
                    }
                }
                else
                {
                    var defaults = new[]
                    {
                        ("Walls", BuiltInCategory.OST_Walls),
                        ("Floors", BuiltInCategory.OST_Floors),
                        ("Columns", BuiltInCategory.OST_Columns),
                        ("StructuralColumns", BuiltInCategory.OST_StructuralColumns),
                        ("StructuralFraming", BuiltInCategory.OST_StructuralFraming),
                        ("StructuralFoundation", BuiltInCategory.OST_StructuralFoundation),
                        ("Roofs", BuiltInCategory.OST_Roofs),
                        ("Ceilings", BuiltInCategory.OST_Ceilings),
                    };
                    foreach (var (name, cat) in defaults)
                    {
                        targetCats.Add(cat);
                        targetNames.Add(name);
                    }
                }

                List<int> elementIds = _elementIds != null ? new List<int>(_elementIds) : new List<int>();
                if (elementIds.Count == 0)
                {
                    var collector = _viewId.HasValue
                        ? new FilteredElementCollector(doc, new ElementId(_viewId.Value))
                        : new FilteredElementCollector(doc);
                    elementIds = collector.OfCategory(sourceCat)
                        .WhereElementIsNotElementType().ToElements()
                        .Select(e => e.Id.GetIntValue()).ToList();
                }

                if (elementIds.Count == 0)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"No elements found for category {_sourceCategory}",
                        Response = new { Success = true, UnjoinedCount = 0, SourceCategory = _sourceCategory, SourceCount = 0, StoredPairs = WallJoinTracker.Count }
                    };
                    return;
                }

                int unjoinedCount = 0;
                var pairsByCategory = new Dictionary<string, int>();
                for (int i = 0; i < targetNames.Count; i++) pairsByCategory[targetNames[i]] = 0;

                var existingKeys = WallJoinTracker.ExistingKeys();

                using (Transaction trans = new Transaction(doc, $"Unjoin {_sourceCategory} Geometry"))
                {
                    trans.Start();

                    foreach (int sourceId in elementIds)
                    {
                        Element source = doc.GetElement(new ElementId(sourceId));
                        if (source == null) continue;

                        BoundingBoxXYZ bbox = source.get_BoundingBox(null);
                        if (bbox == null) continue;

                        XYZ min = bbox.Min - new XYZ(1, 1, 1);
                        XYZ max = bbox.Max + new XYZ(1, 1, 1);
                        Outline outline = new Outline(min, max);
                        var bboxFilter = new BoundingBoxIntersectsFilter(outline);

                        for (int i = 0; i < targetCats.Count; i++)
                        {
                            var neighbors = new FilteredElementCollector(doc)
                                .OfCategory(targetCats[i])
                                .WhereElementIsNotElementType()
                                .WherePasses(bboxFilter).ToElements();

                            int count = WallJoinTracker.TryUnjoinBatch(doc, source, neighbors, existingKeys);
                            unjoinedCount += count;
                            pairsByCategory[targetNames[i]] += count;
                        }
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Unjoined {unjoinedCount} {_sourceCategory}-neighbor join relationship(s)",
                    Response = new
                    {
                        Success = true,
                        UnjoinedCount = unjoinedCount,
                        SourceCategory = _sourceCategory,
                        SourceCount = elementIds.Count,
                        PairsByCategory = pairsByCategory,
                        StoredPairs = WallJoinTracker.Count,
                        Message = $"Unjoined {unjoinedCount} {_sourceCategory}-neighbor join relationship(s)"
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error unjoining elements: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 30000) => _resetEvent.WaitOne(timeoutMilliseconds);
        public string GetName() => "UnjoinElementJoinsEventHandler";
    }
}
