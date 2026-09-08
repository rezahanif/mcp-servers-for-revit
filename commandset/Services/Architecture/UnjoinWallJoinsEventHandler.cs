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
    public class UnjoinWallJoinsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private List<int> _wallIds;
        private int? _viewId;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(List<int> wallIds, int? viewId)
        {
            _wallIds = wallIds;
            _viewId = viewId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;
                List<int> wallIds = _wallIds != null ? new List<int>(_wallIds) : new List<int>();

                if (wallIds.Count == 0 && _viewId.HasValue)
                {
                    var collector = new FilteredElementCollector(doc, new ElementId(_viewId.Value));
                    var walls = collector.OfClass(typeof(Wall)).ToElements();
                    wallIds = walls.Select(w => w.Id.GetIntValue()).ToList();
                }

                if (wallIds.Count == 0)
                    throw new Exception("Please provide wallIds or viewId");

                int unjoinedCount = 0;
                var existingKeys = WallJoinTracker.ExistingKeys();

                using (Transaction trans = new Transaction(doc, "Unjoin Wall Geometry"))
                {
                    trans.Start();

                    foreach (int wallId in wallIds)
                    {
                        Wall wall = doc.GetElement(new ElementId(wallId)) as Wall;
                        if (wall == null) continue;

                        BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
                        if (bbox == null) continue;

                        XYZ min = bbox.Min - new XYZ(1, 1, 1);
                        XYZ max = bbox.Max + new XYZ(1, 1, 1);
                        Outline outline = new Outline(min, max);

                        var columnCollector = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_Columns)
                            .WherePasses(new BoundingBoxIntersectsFilter(outline));

                        var structColumnCollector = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_StructuralColumns)
                            .WherePasses(new BoundingBoxIntersectsFilter(outline));

                        var columns = columnCollector.ToElements().Concat(structColumnCollector.ToElements());

                        unjoinedCount += WallJoinTracker.TryUnjoinBatch(doc, wall, columns, existingKeys);
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Unjoined {unjoinedCount} join relationship(s)",
                    Response = new
                    {
                        Success = true,
                        UnjoinedCount = unjoinedCount,
                        WallCount = wallIds.Count,
                        StoredPairs = WallJoinTracker.Count,
                        Message = $"Unjoined {unjoinedCount} join relationship(s)"
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error unjoining walls: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 30000) => _resetEvent.WaitOne(timeoutMilliseconds);
        public string GetName() => "UnjoinWallJoinsEventHandler";
    }
}
