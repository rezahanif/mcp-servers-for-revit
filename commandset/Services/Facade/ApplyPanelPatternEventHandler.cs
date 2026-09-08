using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Facade
{
    public class ApplyPanelPatternEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                UIDocument uidoc = _uiApp.ActiveUIDocument;

                long? wallElementId = _parameters["elementId"]?.Value<long?>() ?? _parameters["wallId"]?.Value<long?>();
                JObject typeMapping = _parameters["typeMapping"] as JObject;
                JArray matrix = _parameters["matrix"] as JArray;

                Wall wall = null;
                if (wallElementId.HasValue && wallElementId.Value > 0)
                {
#if REVIT2024_OR_GREATER
                    wall = doc.GetElement(new ElementId(wallElementId.Value)) as Wall;
#else
                    wall = doc.GetElement(new ElementId((int)wallElementId.Value)) as Wall;
#endif
                }
                else
                {
                    var selection = uidoc.Selection.GetElementIds();
                    if (selection.Count > 0) wall = doc.GetElement(selection.First()) as Wall;
                }

                if (wall == null) throw new InvalidOperationException("Curtain wall not found");

                CurtainGrid grid = wall.CurtainGrid;
                if (grid == null) throw new InvalidOperationException("Wall has no CurtainGrid");

                var typeMappingDict = new Dictionary<string, long>();
                if (typeMapping != null)
                {
                    foreach (var prop in typeMapping.Properties())
                    {
                        typeMappingDict[prop.Name] = prop.Value.Value<long>();
                    }
                }

                if (matrix == null || matrix.Count == 0) throw new ArgumentException("matrix is required");

                var panelIds = grid.GetPanelIds().ToList();
                var panelPositions = new List<(ElementId Id, XYZ Center)>();

                foreach (ElementId panelId in panelIds)
                {
                    Element panel = doc.GetElement(panelId);
                    if (panel == null) continue;
                    BoundingBoxXYZ bb = panel.get_BoundingBox(null);
                    if (bb == null) continue;
                    panelPositions.Add((panelId, (bb.Min + bb.Max) / 2));
                }

                var sortedByZ = panelPositions.OrderByDescending(p => p.Center.Z).ToList();
                var rowGroups = new List<List<(ElementId Id, XYZ Center)>>();
                double zTolerance = 0.5;

                foreach (var panel in sortedByZ)
                {
                    bool added = false;
                    foreach (var group in rowGroups)
                    {
                        if (Math.Abs(group[0].Center.Z - panel.Center.Z) < zTolerance)
                        {
                            group.Add((panel.Id, panel.Center));
                            added = true;
                            break;
                        }
                    }
                    if (!added) rowGroups.Add(new List<(ElementId, XYZ)> { (panel.Id, panel.Center) });
                }

                var panelGrid = new Dictionary<(int row, int col), ElementId>();
                int rowIndex = 0;
                foreach (var rowGroup in rowGroups)
                {
                    var sortedRow = rowGroup.OrderBy(p => p.Center.X).ThenBy(p => p.Center.Y).ToList();
                    int colIndex = 0;
                    foreach (var panel in sortedRow)
                    {
                        panelGrid[(rowIndex, colIndex)] = panel.Id;
                        colIndex++;
                    }
                    rowIndex++;
                }

                int successCount = 0;
                int failCount = 0;
                var failedPanels = new List<object>();

                using (Transaction trans = new Transaction(doc, "Apply Panel Pattern"))
                {
                    trans.Start();

                    for (int r = 0; r < matrix.Count && r < rowGroups.Count; r++)
                    {
                        JArray rowData = matrix[r] as JArray;
                        if (rowData == null) continue;

                        for (int c = 0; c < rowData.Count; c++)
                        {
                            if (!panelGrid.ContainsKey((r, c))) continue;

                            long targetTypeId = 0;
                            var cellValue = rowData[c];
                            if (cellValue.Type == JTokenType.String)
                            {
                                string key = cellValue.Value<string>();
                                if (!string.IsNullOrEmpty(key) && !typeMappingDict.TryGetValue(key, out targetTypeId))
                                {
                                    failedPanels.Add(new { Row = r, Col = c, Reason = $"No mapping for key {key}" });
                                    failCount++;
                                    continue;
                                }
                            }
                            else if (cellValue.Type == JTokenType.Integer)
                            {
                                targetTypeId = cellValue.Value<long>();
                            }

                            if (targetTypeId == 0) continue;

                            ElementId panelId = panelGrid[(r, c)];
                            Element panel = doc.GetElement(panelId);
                            if (panel == null) { failCount++; continue; }

                            try
                            {
#if REVIT2024_OR_GREATER
                                panel.ChangeTypeId(new ElementId(targetTypeId));
#else
                                panel.ChangeTypeId(new ElementId((int)targetTypeId));
#endif
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                failedPanels.Add(new { PanelId = panelId.GetIntValue(), Row = r, Col = c, Reason = ex.Message });
                                failCount++;
                            }
                        }
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully applied pattern to {successCount} panels, {failCount} failed",
                    Response = new
                    {
                        WallId = wall.Id.GetIntValue(),
                        TotalPanels = panelIds.Count,
                        SuccessCount = successCount,
                        FailCount = failCount,
                        FailedPanels = failedPanels
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error applying panel pattern: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "ApplyPanelPatternEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
