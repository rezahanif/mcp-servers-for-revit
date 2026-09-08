using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.AnnotationComponents
{
    public class CreateCorridorDimensionEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                long roomId = _parameters?["roomId"]?.Value<long>() ?? 0;
                long viewId = _parameters?["viewId"]?.Value<long>() ?? 0;

                Room room = doc.GetElement(roomId.ToElementId()) as Room;
                if (room == null) throw new ArgumentException($"Room not found: {roomId}");

                View view = doc.GetElement(viewId.ToElementId()) as View;
                if (view == null) throw new ArgumentException($"View not found: {viewId}");

                var bOptions = new SpatialElementBoundaryOptions();
                bOptions.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;
                var segmentLoops = room.GetBoundarySegments(bOptions);

                if (segmentLoops == null || segmentLoops.Count == 0)
                    throw new InvalidOperationException("Room has no boundary segments");

                var lines = new List<Line>();
                foreach (var seg in segmentLoops[0])
                {
                    var curve = seg.GetCurve();
                    if (curve is Line line && line.Length > 0.3)
                        lines.Add(line);
                }

                if (lines.Count < 2)
                    throw new InvalidOperationException($"Insufficient boundary line segments (only {lines.Count} straight lines)");

                var pairs = new List<int[]>();
                var pairWidths = new List<double>();
                var pairAvgLens = new List<double>();

                for (int i = 0; i < lines.Count; i++)
                {
                    XYZ dir1 = lines[i].Direction.Normalize();
                    double len1 = lines[i].Length;

                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        XYZ dir2 = lines[j].Direction.Normalize();
                        double len2 = lines[j].Length;

                        double dot = Math.Abs(dir1.DotProduct(dir2));
                        if (dot < 0.996) continue;

                        XYZ perp = new XYZ(-dir1.Y, dir1.X, 0).Normalize();
                        XYZ diff = lines[j].GetEndPoint(0).Subtract(lines[i].GetEndPoint(0));
                        double dist = Math.Abs(diff.DotProduct(perp));

                        if (dist < 100.0 / 304.8) continue;

                        double avgLen = (len1 + len2) / 2;
                        if (avgLen < dist) continue;

                        double s1a = lines[i].GetEndPoint(0).DotProduct(dir1);
                        double s1b = lines[i].GetEndPoint(1).DotProduct(dir1);
                        double s2a = lines[j].GetEndPoint(0).DotProduct(dir1);
                        double s2b = lines[j].GetEndPoint(1).DotProduct(dir1);

                        double min1 = Math.Min(s1a, s1b), max1 = Math.Max(s1a, s1b);
                        double min2 = Math.Min(s2a, s2b), max2 = Math.Max(s2a, s2b);
                        double oStart = Math.Max(min1, min2);
                        double oEnd = Math.Min(max1, max2);
                        if (oEnd <= oStart + 0.01) continue;

                        pairs.Add(new[] { i, j });
                        pairWidths.Add(dist);
                        pairAvgLens.Add(avgLen);
                    }
                }

                if (pairs.Count == 0)
                    throw new InvalidOperationException("No parallel wall pairs found (room might not be a corridor shape)");

                var sorted = Enumerable.Range(0, pairs.Count)
                    .OrderByDescending(k => pairAvgLens[k])
                    .ToList();

                var measurements = new List<object>();
                var widthValues = new List<double>();

                using (Transaction trans = new Transaction(doc, "Create Corridor Dimension"))
                {
                    trans.Start();

                    foreach (int k in sorted)
                    {
                        var line1 = lines[pairs[k][0]];
                        var line2 = lines[pairs[k][1]];
                        XYZ dir = line1.Direction.Normalize();

                        double s1a = line1.GetEndPoint(0).DotProduct(dir);
                        double s1b = line1.GetEndPoint(1).DotProduct(dir);
                        double s2a = line2.GetEndPoint(0).DotProduct(dir);
                        double s2b = line2.GetEndPoint(1).DotProduct(dir);

                        double min1 = Math.Min(s1a, s1b), max1 = Math.Max(s1a, s1b);
                        double min2 = Math.Min(s2a, s2b), max2 = Math.Max(s2a, s2b);
                        double oMid = (Math.Max(min1, min2) + Math.Min(max1, max2)) / 2;

                        double t1 = (s1b != s1a) ? (oMid - s1a) / (s1b - s1a) : 0.5;
                        double t2 = (s2b != s2a) ? (oMid - s2a) / (s2b - s2a) : 0.5;
                        t1 = Math.Max(0.01, Math.Min(0.99, t1));
                        t2 = Math.Max(0.01, Math.Min(0.99, t2));

                        XYZ p1 = line1.Evaluate(t1, true);
                        XYZ p2 = line2.Evaluate(t2, true);

                        double tickLen = 0.5;
                        DetailCurve dc1 = doc.Create.NewDetailCurve(view,
                            Line.CreateBound(p1.Subtract(dir.Multiply(tickLen)), p1.Add(dir.Multiply(tickLen))));
                        DetailCurve dc2 = doc.Create.NewDetailCurve(view,
                            Line.CreateBound(p2.Subtract(dir.Multiply(tickLen)), p2.Add(dir.Multiply(tickLen))));

                        double offsetFt = 1.5;
                        Line dimLine = Line.CreateBound(
                            p1.Add(dir.Multiply(offsetFt)),
                            p2.Add(dir.Multiply(offsetFt)));

                        ReferenceArray refArray = new ReferenceArray();
                        refArray.Append(dc1.GeometryCurve.Reference);
                        refArray.Append(dc2.GeometryCurve.Reference);

                        Dimension dim = doc.Create.NewDimension(view, dimLine, refArray);

                        double widthMm = dim.Value.HasValue ? dim.Value.Value * 304.8 : pairWidths[k] * 304.8;
                        widthMm = Math.Round(widthMm, 0);
                        widthValues.Add(widthMm);

                        measurements.Add(new
                        {
                            SegmentIndex = measurements.Count + 1,
                            Width = widthMm,
                            Length = Math.Round(pairAvgLens[k] * 304.8, 0),
                            DimensionId = dim.Id.GetIntValue(),
                            Point1 = new { X = Math.Round(p1.X * 304.8, 0), Y = Math.Round(p1.Y * 304.8, 0) },
                            Point2 = new { X = Math.Round(p2.X * 304.8, 0), Y = Math.Round(p2.Y * 304.8, 0) },
                            Method = "boundary_accurate",
                            Compliant_1600 = widthMm >= 1600,
                            Compliant_1200 = widthMm >= 1200
                        });
                    }

                    trans.Commit();
                }

                string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";
                double minWidth = widthValues.Count > 0 ? widthValues.Min() : 0;

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully created {measurements.Count} corridor dimensions",
                    Response = new
                    {
                        RoomId = roomId,
                        RoomName = roomName,
                        RoomNumber = roomNumber,
                        Level = room.Level?.Name ?? "",
                        TotalSegments = measurements.Count,
                        MinWidth = minWidth,
                        AllPass_1600 = widthValues.All(w => w >= 1600),
                        AllPass_1200 = widthValues.All(w => w >= 1200),
                        Segments = measurements
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error creating corridor dimensions: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "CreateCorridorDimensionEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
