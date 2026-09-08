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
    public class QueryWallsByLocationEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        private double _centerX;
        private double _centerY;
        private double _searchRadius;
        private string _levelName;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(double x, double y, double searchRadius, string level)
        {
            _centerX = x;
            _centerY = y;
            _searchRadius = searchRadius;
            _levelName = level;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                XYZ center = new XYZ(_centerX / 304.8, _centerY / 304.8, 0);

                var wallCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType()
                    .Cast<Wall>();

                if (!string.IsNullOrEmpty(_levelName))
                {
                    var level = new FilteredElementCollector(doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .FirstOrDefault(l => l.Name.IndexOf(_levelName, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (level != null)
                    {
                        wallCollector = wallCollector.Where(w =>
                        {
                            ElementId baseLevelId = w.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId();
                            if (baseLevelId != null && baseLevelId != ElementId.InvalidElementId)
                                return baseLevelId == level.Id;
                            return w.LevelId == level.Id;
                        });
                    }
                }

                var nearbyWalls = new List<object>();

                foreach (var wall in wallCollector)
                {
                    LocationCurve locCurve = wall.Location as LocationCurve;
                    if (locCurve == null) continue;

                    Curve curve = locCurve.Curve;
                    XYZ startPoint = curve.GetEndPoint(0);
                    XYZ endPoint = curve.GetEndPoint(1);

                    XYZ startFlat = new XYZ(startPoint.X, startPoint.Y, 0);
                    XYZ endFlat = new XYZ(endPoint.X, endPoint.Y, 0);

                    XYZ wallDir = (endFlat - startFlat).Normalize();
                    XYZ toCenter = center - startFlat;
                    double proj = toCenter.DotProduct(wallDir);
                    double wallLength = startFlat.DistanceTo(endFlat);

                    XYZ closestPoint;
                    if (proj < 0)
                        closestPoint = startFlat;
                    else if (proj > wallLength)
                        closestPoint = endFlat;
                    else
                        closestPoint = startFlat + wallDir * proj;

                    double distToWall = center.DistanceTo(closestPoint) * 304.8;

                    if (distToWall <= _searchRadius)
                    {
                        double thickness = wall.Width * 304.8;
                        XYZ perpendicular = new XYZ(-wallDir.Y, wallDir.X, 0);
                        double halfThickness = wall.Width / 2;

                        XYZ face1Point = closestPoint + perpendicular * halfThickness;
                        XYZ face2Point = closestPoint - perpendicular * halfThickness;

                        nearbyWalls.Add(new
                        {
                            ElementId = wall.Id.GetIntValue(),
                            Name = wall.Name,
                            WallType = wall.WallType.Name,
                            Thickness = Math.Round(thickness, 2),
                            Length = Math.Round(curve.Length * 304.8, 2),
                            DistanceToCenter = Math.Round(distToWall, 2),
                            LocationLine = new
                            {
                                StartX = Math.Round(startPoint.X * 304.8, 2),
                                StartY = Math.Round(startPoint.Y * 304.8, 2),
                                EndX = Math.Round(endPoint.X * 304.8, 2),
                                EndY = Math.Round(endPoint.Y * 304.8, 2)
                            },
                            ClosestPoint = new
                            {
                                X = Math.Round(closestPoint.X * 304.8, 2),
                                Y = Math.Round(closestPoint.Y * 304.8, 2)
                            },
                            Face1 = new
                            {
                                X = Math.Round(face1Point.X * 304.8, 2),
                                Y = Math.Round(face1Point.Y * 304.8, 2)
                            },
                            Face2 = new
                            {
                                X = Math.Round(face2Point.X * 304.8, 2),
                                Y = Math.Round(face2Point.Y * 304.8, 2)
                            },
                            Orientation = Math.Abs(wallDir.X) > Math.Abs(wallDir.Y) ? "Horizontal" : "Vertical"
                        });
                    }
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Found {nearbyWalls.Count} walls within {_searchRadius} mm",
                    Response = new
                    {
                        Count = nearbyWalls.Count,
                        Walls = nearbyWalls
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error querying walls by location: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "QueryWallsByLocationEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
