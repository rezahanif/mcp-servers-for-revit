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
    public class JoinWallTopsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private List<string> _levels;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(List<string> levels)
        {
            _levels = levels;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                List<Level> allLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(lv => lv.Elevation).ToList();

                if (allLevels.Count == 0)
                    throw new Exception("Project has no levels");

                List<Level> selectedLevels;
                if (_levels == null || _levels.Count == 0)
                {
                    selectedLevels = allLevels;
                }
                else
                {
                    selectedLevels = allLevels
                        .Where(lv => _levels.Any(n => lv.Name == n || lv.Name.Contains(n) || n.Contains(lv.Name)))
                        .ToList();

                    if (selectedLevels.Count == 0)
                        throw new Exception($"No matching levels found for: {string.Join(", ", _levels)}");
                }

                var selectedLevelIds = new HashSet<ElementId>(selectedLevels.Select(lv => lv.Id));

                List<Wall> walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall)).Cast<Wall>()
                    .Where(w =>
                    {
                        Parameter p = w.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                        return p != null && selectedLevelIds.Contains(p.AsElementId());
                    })
                    .ToList();

                if (walls.Count == 0)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = "No walls found on the selected levels",
                        Response = new { Success = true, ProcessedLevels = selectedLevels.Count, ProcessedWalls = 0, Joined = 0, Skipped = 0, Failed = 0 }
                    };
                    return;
                }

                var categoryFilter = new ElementMulticategoryFilter(new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_Ceilings,
                    BuiltInCategory.OST_StructuralFraming
                });

                int joinedCount = 0, skippedCount = 0, failedCount = 0;
                var failedInfos = new List<string>();
                var levelStats = selectedLevels.ToDictionary(lv => lv.Name, lv => 0);

                using (Transaction trans = new Transaction(doc, "Join Wall Tops"))
                {
                    trans.Start();

                    foreach (Wall wall in walls)
                    {
                        BoundingBoxXYZ wallBB = wall.get_BoundingBox(null);
                        if (wallBB == null) continue;

                        Parameter baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                        Level baseLevel = doc.GetElement(baseLevelParam.AsElementId()) as Level;
                        string levelName = baseLevel?.Name ?? "Unknown";

                        double searchDown = 1.0, searchUp = 2.0, hTol = 0.1;
                        Outline searchOutline = new Outline(
                            new XYZ(wallBB.Min.X - hTol, wallBB.Min.Y - hTol, wallBB.Max.Z - searchDown),
                            new XYZ(wallBB.Max.X + hTol, wallBB.Max.Y + hTol, wallBB.Max.Z + searchUp));

                        var targets = new FilteredElementCollector(doc)
                            .WherePasses(categoryFilter)
                            .WherePasses(new BoundingBoxIntersectsFilter(searchOutline))
                            .WherePasses(new ElementIntersectsElementFilter(wall))
                            .WhereElementIsNotElementType()
                            .ToList();

                        foreach (Element target in targets)
                        {
                            try
                            {
                                if (JoinGeometryUtils.AreElementsJoined(doc, wall, target))
                                {
                                    skippedCount++;
                                    continue;
                                }

                                JoinGeometryUtils.JoinGeometry(doc, wall, target);
                                joinedCount++;
                                if (levelStats.ContainsKey(levelName))
                                    levelStats[levelName]++;
                            }
                            catch
                            {
                                failedCount++;
                                string categoryName = target.Category?.Name ?? "Unknown Category";
                                failedInfos.Add($"[{levelName}] Wall ID:{wall.Id.GetIntValue()} <-> {categoryName} ID:{target.Id.GetIntValue()}");
                            }
                        }
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully joined {joinedCount} pair(s), skipped {skippedCount} already joined, failed {failedCount}",
                    Response = new
                    {
                        Success = true,
                        ProcessedLevels = selectedLevels.Count,
                        ProcessedWalls = walls.Count,
                        Joined = joinedCount,
                        Skipped = skippedCount,
                        Failed = failedCount,
                        LevelStats = levelStats.Select(kv => new { Level = kv.Key, Joined = kv.Value }).ToList(),
                        Failures = failedInfos,
                        Message = $"Successfully joined {joinedCount} pair(s), skipped {skippedCount} already joined, failed {failedCount}"
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error joining wall tops: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 60000) => _resetEvent.WaitOne(timeoutMilliseconds);
        public string GetName() => "JoinWallTopsEventHandler";
    }
}
