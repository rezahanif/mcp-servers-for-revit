using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    public class TraceStairGeometryEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                View view = uiapp.ActiveUIDocument.ActiveView;

                XYZ viewDir = view.ViewDirection;
                XYZ origin = view.Origin;

                var runs = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_StairsRuns)
                    .WhereElementIsNotElementType()
                    .Cast<StairsRun>()
                    .ToList();

                var results = new List<object>();

                foreach (var run in runs)
                {
                    Stairs parentStair = run.GetStairs();
                    if (parentStair != null)
                    {
                        ElementType stairType = doc.GetElement(parentStair.GetTypeId()) as ElementType;
                        if (stairType != null)
                        {
                            string familyName = stairType.FamilyName ?? "";
                            if (!familyName.Contains("組合") && !familyName.Contains("Assembled"))
                                continue;
                        }
                    }

                    Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
                    GeometryElement geom = run.get_Geometry(opt);

                    var hiddenLines = new List<object>();
                    var allEdges = new List<StairEdgeData>();

                    CollectStairEdges(geom, null, origin, viewDir, allEdges);

                    if (allEdges.Count > 0)
                    {
                        double minDepth = allEdges.Min(e => e.Depth);

                        if (minDepth > 0.05)
                        {
                            double depthTolerance = 2.5;
                            var firstRunEdges = allEdges.Where(e => e.Depth <= minDepth + depthTolerance && e.IsStepProfile).ToList();

                            foreach (var edge in firstRunEdges)
                            {
                                hiddenLines.Add(new
                                {
                                    startX = Math.Round(edge.P0.X * 304.8, 2),
                                    startY = Math.Round(edge.P0.Y * 304.8, 2),
                                    startZ = Math.Round(edge.P0.Z * 304.8, 2),
                                    endX = Math.Round(edge.P1.X * 304.8, 2),
                                    endY = Math.Round(edge.P1.Y * 304.8, 2),
                                    endZ = Math.Round(edge.P1.Z * 304.8, 2)
                                });
                            }
                        }
                    }

                    if (hiddenLines.Count > 0)
                    {
                        results.Add(new
                        {
                            StairId = run.Id.GetIntValue(),
                            HiddenLines = hiddenLines,
                            TotalEdges = allEdges.Count,
                            FirstRunEdgesCount = hiddenLines.Count
                        });
                    }
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Traced {results.Count} stair run(s)",
                    Response = new { Count = results.Count, Stairs = results }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error tracing stair geometry: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private void CollectStairEdges(GeometryElement gelem, Transform transform, XYZ origin, XYZ viewDir, List<StairEdgeData> allEdges)
        {
            if (gelem == null) return;
            foreach (GeometryObject obj in gelem)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        Curve c = edge.AsCurve();
                        if (transform != null && !transform.IsIdentity)
                            c = c.CreateTransformed(transform);

                        if (c.Length < 0.01) continue;

                        XYZ p0 = c.GetEndPoint(0);
                        XYZ p1 = c.GetEndPoint(1);
                        XYZ mid = c.Evaluate(0.5, true);

                        double depth = (origin - mid).DotProduct(viewDir);
                        double d0 = (origin - p0).DotProduct(viewDir);
                        double d1 = (origin - p1).DotProduct(viewDir);
                        XYZ proj0 = p0 + viewDir * d0;
                        XYZ proj1 = p1 + viewDir * d1;
                        double projLen = proj0.DistanceTo(proj1);

                        if (projLen < 0.01) continue;

                        XYZ dir = (proj1 - proj0).Normalize();
                        bool isHorizontal = Math.Abs(dir.Z) < 0.1;
                        bool isVertical = Math.Abs(Math.Abs(dir.Z) - 1.0) < 0.1;
                        bool isStepProfile = isHorizontal || isVertical || (projLen < 0.65);

                        allEdges.Add(new StairEdgeData
                        {
                            Depth = depth,
                            Length = projLen,
                            IsStepProfile = isStepProfile,
                            P0 = p0,
                            P1 = p1
                        });
                    }
                }
                else if (obj is GeometryInstance instance)
                {
                    Transform currentTransform = transform != null ? transform.Multiply(instance.Transform) : instance.Transform;
                    CollectStairEdges(instance.SymbolGeometry, currentTransform, origin, viewDir, allEdges);
                }
            }
        }

        private class StairEdgeData
        {
            public double Depth;
            public double Length;
            public bool IsStepProfile;
            public XYZ P0;
            public XYZ P1;
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "TraceStairGeometryEventHandler";
    }
}
