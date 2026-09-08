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
    public class UnjoinColumnJoinsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private List<int> _columnIds;
        private int? _viewId;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(List<int> columnIds, int? viewId)
        {
            _columnIds = columnIds;
            _viewId = viewId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;
                List<int> columnIds = _columnIds != null ? new List<int>(_columnIds) : new List<int>();

                if (columnIds.Count == 0)
                {
                    FilteredElementCollector archCols = _viewId.HasValue
                        ? new FilteredElementCollector(doc, new ElementId(_viewId.Value))
                        : new FilteredElementCollector(doc);
                    FilteredElementCollector structCols = _viewId.HasValue
                        ? new FilteredElementCollector(doc, new ElementId(_viewId.Value))
                        : new FilteredElementCollector(doc);

                    var arch = archCols.OfCategory(BuiltInCategory.OST_Columns)
                        .WhereElementIsNotElementType().ToElements();
                    var stru = structCols.OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsNotElementType().ToElements();

                    columnIds = arch.Concat(stru).Select(c => c.Id.GetIntValue()).ToList();
                }

                if (columnIds.Count == 0)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = "No columns found",
                        Response = new { Success = true, UnjoinedCount = 0, ColumnCount = 0, StoredPairs = WallJoinTracker.Count }
                    };
                    return;
                }

                int unjoinedCount = 0;
                int wallPairs = 0;
                int floorPairs = 0;
                int framingPairs = 0;

                var existingKeys = WallJoinTracker.ExistingKeys();

                using (Transaction trans = new Transaction(doc, "Unjoin Column Geometry"))
                {
                    trans.Start();

                    foreach (int columnId in columnIds)
                    {
                        Element column = doc.GetElement(new ElementId(columnId));
                        if (column == null) continue;

                        BoundingBoxXYZ bbox = column.get_BoundingBox(null);
                        if (bbox == null) continue;

                        XYZ min = bbox.Min - new XYZ(1, 1, 1);
                        XYZ max = bbox.Max + new XYZ(1, 1, 1);
                        Outline outline = new Outline(min, max);
                        var bboxFilter = new BoundingBoxIntersectsFilter(outline);

                        var wallNeighbors = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_Walls)
                            .WhereElementIsNotElementType()
                            .WherePasses(bboxFilter).ToElements();

                        var floorNeighbors = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_Floors)
                            .WhereElementIsNotElementType()
                            .WherePasses(bboxFilter).ToElements();

                        var framingNeighbors = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_StructuralFraming)
                            .WhereElementIsNotElementType()
                            .WherePasses(bboxFilter).ToElements();

                        int wCount = WallJoinTracker.TryUnjoinBatch(doc, column, wallNeighbors, existingKeys);
                        wallPairs += wCount;
                        int fCount = WallJoinTracker.TryUnjoinBatch(doc, column, floorNeighbors, existingKeys);
                        floorPairs += fCount;
                        int frCount = WallJoinTracker.TryUnjoinBatch(doc, column, framingNeighbors, existingKeys);
                        framingPairs += frCount;

                        unjoinedCount += (wCount + fCount + frCount);
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Unjoined {unjoinedCount} column-neighbor join relationship(s)",
                    Response = new
                    {
                        Success = true,
                        UnjoinedCount = unjoinedCount,
                        ColumnCount = columnIds.Count,
                        WallPairs = wallPairs,
                        FloorPairs = floorPairs,
                        FramingPairs = framingPairs,
                        StoredPairs = WallJoinTracker.Count,
                        Message = $"Unjoined {unjoinedCount} column-neighbor join relationship(s)"
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error unjoining columns: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 30000) => _resetEvent.WaitOne(timeoutMilliseconds);
        public string GetName() => "UnjoinColumnJoinsEventHandler";
    }
}
