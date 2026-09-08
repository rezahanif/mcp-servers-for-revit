using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Utils
{
    public static class FloorSlopeAnalyzer
    {
        private const double UpwardNormalThreshold = 0.7;

        public static object Run(Document doc, JObject parameters)
        {
            if (doc == null)
                return new { Success = false, Message = "No active document." };

            string paramName = parameters?["paramName"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(paramName))
                paramName = "Comments";

            List<Floor> floors = CollectFloors(doc, parameters);
            if (floors.Count == 0)
            {
                return new
                {
                    Success = false,
                    Message = "No matching floors found. When elementIds is omitted, floors with Function=Exterior are collected."
                };
            }

            var results = new List<object>();
            var errors = new List<string>();
            int processed = 0;

            using (Transaction trans = new Transaction(doc, "Analyze Floor Slopes"))
            {
                trans.Start();

                foreach (Floor floor in floors)
                {
                    try
                    {
                        var slope = ComputeSlope(floor);
                        if (slope == null)
                        {
                            errors.Add($"Floor {floor.Id.GetIntValue()} has no upward planar faces to analyze.");
                            continue;
                        }

                        double minPct = slope.Item1;
                        double maxPct = slope.Item2;
                        int faceCount = slope.Item3;

                        string written = null;
                        Parameter p = floor.LookupParameter(paramName);
                        if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                        {
                            written = $"Slope {minPct:F2}%~{maxPct:F2}%";
                            p.Set(written);
                        }
                        else if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                        {
                            p.Set(maxPct / 100.0);
                            written = $"{maxPct:F2}% (max, numeric)";
                        }
                        else
                        {
                            errors.Add($"Floor {floor.Id.GetIntValue()} parameter '{paramName}' not found or read-only.");
                        }

                        results.Add(new
                        {
                            ElementId = floor.Id.GetIntValue(),
                            MinSlopePercent = Math.Round(minPct, 2),
                            MaxSlopePercent = Math.Round(maxPct, 2),
                            UpwardFaceCount = faceCount,
                            Written = written
                        });
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Floor {floor.Id.GetIntValue()} analysis failed: {ex.Message}");
                    }
                }

                trans.Commit();
            }

            return new
            {
                Success = errors.Count == 0,
                ProcessedCount = processed,
                ParameterName = paramName,
                Floors = results,
                Errors = errors
            };
        }

        private static List<Floor> CollectFloors(Document doc, JObject parameters)
        {
            var ids = parameters?["elementIds"] as JArray;
            if (ids != null && ids.Count > 0)
            {
                var list = new List<Floor>();
                foreach (var token in ids)
                {
                    try
                    {
                        long v = token.Value<long>();
                        if (doc.GetElement(v.ToElementId()) is Floor f)
                            list.Add(f);
                    }
                    catch
                    {
                    }
                }
                return list;
            }

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .Where(f => IsExterior(doc, f))
                .ToList();
        }

        private static bool IsExterior(Document doc, Floor floor)
        {
            Element type = doc.GetElement(floor.GetTypeId());
            Parameter fp = type?.get_Parameter(BuiltInParameter.FUNCTION_PARAM);
            return fp != null && fp.AsInteger() == 1; // 0 = Interior, 1 = Exterior
        }

        private static Tuple<double, double, int> ComputeSlope(Floor floor)
        {
            var opt = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geo = floor.get_Geometry(opt);
            if (geo == null) return null;

            var slopes = new List<double>();
            CollectUpwardSlopes(geo, slopes);

            if (slopes.Count == 0) return null;
            return Tuple.Create(slopes.Min(), slopes.Max(), slopes.Count);
        }

        private static void CollectUpwardSlopes(IEnumerable<GeometryObject> geometry, List<double> slopes)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid)
                {
                    if (solid.Faces.Size == 0) continue;
                    foreach (Face face in solid.Faces)
                    {
                        if (!(face is PlanarFace pf)) continue;

                        XYZ n = pf.FaceNormal;
                        if (n.GetLength() < 1e-9) continue;
                        n = n.Normalize();

                        if (n.Z <= UpwardNormalThreshold) continue;

                        double cos = Math.Min(1.0, Math.Max(-1.0, n.Z));
                        double slopePct = Math.Tan(Math.Acos(cos)) * 100.0;
                        slopes.Add(slopePct);
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    CollectUpwardSlopes(gi.GetInstanceGeometry(), slopes);
                }
            }
        }
    }
}
