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
    public class CreateFacadeFromAnalysisEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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

                long? wallId = _parameters["wallId"]?.Value<long?>();
                JObject facadeLayers = _parameters["facadeLayers"] as JObject;
                if (facadeLayers == null) throw new ArgumentException("facadeLayers is required");

                JObject outerLayer = facadeLayers["outer"] as JObject;
                if (outerLayer == null) throw new ArgumentException("facadeLayers.outer is required");

                double globalOffset = outerLayer["offset"]?.Value<double>() ?? 200;
                double gap = outerLayer["gap"]?.Value<double>() ?? 20;
                double bandHeight = outerLayer["horizontalBandHeight"]?.Value<double>() ?? 0;
                double floorHeight = outerLayer["floorHeight"]?.Value<double>() ?? 3600;

                JArray panelTypesArray = outerLayer["panelTypes"] as JArray;
                JArray patternArray = outerLayer["pattern"] as JArray;

                if (panelTypesArray == null || panelTypesArray.Count == 0) throw new ArgumentException("panelTypes must not be empty");
                if (patternArray == null || patternArray.Count == 0) throw new ArgumentException("pattern must not be empty");

                Wall wall = null;
                if (wallId.HasValue && wallId.Value > 0)
                {
#if REVIT2024_OR_GREATER
                    wall = doc.GetElement(new ElementId(wallId.Value)) as Wall;
#else
                    wall = doc.GetElement(new ElementId((int)wallId.Value)) as Wall;
#endif
                }
                else
                {
                    var selection = uidoc.Selection.GetElementIds();
                    if (selection.Count > 0) wall = doc.GetElement(selection.First()) as Wall;
                }

                if (wall == null) throw new InvalidOperationException("Target wall not found");

                LocationCurve wallLoc = wall.Location as LocationCurve;
                if (wallLoc == null) throw new InvalidOperationException("Cannot get wall location curve");
                Line wallLine = wallLoc.Curve as Line;
                if (wallLine == null) throw new InvalidOperationException("Only straight walls are supported");

                XYZ wallDir = wallLine.Direction.Normalize();
                XYZ wallNormal = wall.Orientation.Normalize();
                double halfWallThickness = wall.Width / 2.0;
                XYZ wallStart = wallLine.GetEndPoint(0) + wallNormal * halfWallThickness;
                double wallLength = wallLine.Length * 304.8;

                Level baseLevel = doc.GetElement(wall.LevelId) as Level;
                double wallBaseZ = baseLevel != null ? baseLevel.Elevation : 0;
                Parameter baseOffsetParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET);
                double baseOffset = baseOffsetParam != null ? baseOffsetParam.AsDouble() : 0;
                double wallBaseElevationMm = (wallBaseZ + baseOffset) * 304.8;

                var typeDict = new Dictionary<string, JObject>();
                foreach (JObject ptObj in panelTypesArray)
                {
                    string id = ptObj["id"]?.Value<string>();
                    if (!string.IsNullOrEmpty(id)) typeDict[id] = ptObj;
                }

                int successCount = 0;
                int failCount = 0;
                var createdPanels = new List<object>();
                var failedPanels = new List<object>();

                using (Transaction trans = new Transaction(doc, "Create Facade From Analysis"))
                {
                    trans.Start();

                    var materialCache = new Dictionary<string, Material>();
                    foreach (var kvp in typeDict)
                    {
                        string colorHex = kvp.Value["color"]?.Value<string>() ?? "#808080";
                        string userName = kvp.Value["name"]?.Value<string>() ?? $"FP_{kvp.Key}";
                        materialCache[kvp.Key] = FacadeGeometryHelper.FindOrCreateFacadeMaterial(doc, colorHex, userName);
                    }

                    for (int floor = 0; floor < patternArray.Count; floor++)
                    {
                        string rowPattern = patternArray[floor]?.Value<string>() ?? "";
                        if (string.IsNullOrEmpty(rowPattern)) continue;

                        double panelH = floorHeight - bandHeight;
                        double zBase = wallBaseElevationMm + floor * floorHeight;

                        double totalRowWidth = 0;
                        for (int c = 0; c < rowPattern.Length; c++)
                        {
                            string typeId = rowPattern[c].ToString();
                            if (typeDict.ContainsKey(typeId))
                            {
                                totalRowWidth += typeDict[typeId]["width"]?.Value<double>() ?? 800;
                                if (c < rowPattern.Length - 1) totalRowWidth += gap;
                            }
                        }

                        double startX = (wallLength - totalRowWidth) / 2;
                        double x = startX;

                        for (int col = 0; col < rowPattern.Length; col++)
                        {
                            string typeId = rowPattern[col].ToString();
                            if (!typeDict.ContainsKey(typeId)) continue;

                            JObject pt = typeDict[typeId];
                            double pw = pt["width"]?.Value<double>() ?? 800;
                            double pd = pt["depth"]?.Value<double>() ?? 150;
                            double pThick = pt["thickness"]?.Value<double>() ?? 30;
                            string pCurve = pt["curveType"]?.Value<string>() ?? "concave";
                            string pColor = pt["color"]?.Value<string>() ?? "#808080";
                            string pGeomType = pt["geometryType"]?.Value<string>() ?? "curved_panel";

                            double pTiltAngle = pt["tiltAngle"]?.Value<double>() ?? 15;
                            string pTiltAxis = pt["tiltAxis"]?.Value<string>() ?? "horizontal";
                            double pCornerRadius = pt["cornerRadius"]?.Value<double>() ?? 100;
                            string pOpeningShape = pt["openingShape"]?.Value<string>() ?? "rounded_rect";
                            string pBevelDir = pt["bevelDirection"]?.Value<string>() ?? "center";
                            double pOpenW = pt["openingWidth"]?.Value<double>() ?? (pw * 0.7);
                            double pOpenH = pt["openingHeight"]?.Value<double>() ?? (panelH * 0.7);

                            try
                            {
                                double posAlongMm = x + pw / 2;

                                Solid solid;
                                switch (pGeomType)
                                {
                                    case "beveled_opening":
                                        solid = FacadeGeometryHelper.CreateBeveledOpeningSolid(
                                            wallStart, wallDir, wallNormal,
                                            posAlongMm, zBase, pw, panelH,
                                            pOpenW, pOpenH, pd, pThick,
                                            pBevelDir, globalOffset);
                                        break;
                                    case "angled_panel":
                                        solid = FacadeGeometryHelper.CreateAngledPanelSolid(
                                            wallStart, wallDir, wallNormal,
                                            posAlongMm, zBase, pw, panelH,
                                            pThick, globalOffset, pTiltAngle, pTiltAxis);
                                        break;
                                    case "rounded_opening":
                                        solid = FacadeGeometryHelper.CreateRoundedOpeningSolid(
                                            wallStart, wallDir, wallNormal,
                                            posAlongMm, zBase, pw, panelH,
                                            pOpenW, pOpenH, pd, pThick,
                                            pCornerRadius, pOpeningShape, globalOffset);
                                        break;
                                    case "flat_panel":
                                        solid = FacadeGeometryHelper.CreateFlatPanelSolid(
                                            wallStart, wallDir, wallNormal,
                                            posAlongMm, zBase, pw, panelH,
                                            pThick, globalOffset);
                                        break;
                                    case "curved_panel":
                                    default:
                                        solid = FacadeGeometryHelper.CreateCurvedPanelSolid(
                                            wallStart, wallDir, wallNormal,
                                            posAlongMm, zBase, pw, panelH,
                                            pd, pThick, pCurve, globalOffset);
                                        break;
                                }

                                string dsName = $"FP_{typeId}_F{floor + 1}_C{col + 1}";
#if REVIT2024_OR_GREATER
                                DirectShape ds = DirectShape.CreateElement(doc, new ElementId((long)BuiltInCategory.OST_GenericModel));
#else
                                DirectShape ds = DirectShape.CreateElement(doc, new ElementId((int)BuiltInCategory.OST_GenericModel));
#endif
                                ds.ApplicationId = "RevitMCP_FacadePanel";
                                ds.ApplicationDataId = dsName;
                                ds.SetShape(new GeometryObject[] { solid });

                                if (materialCache.ContainsKey(typeId))
                                {
                                    FacadeGeometryHelper.ApplyMaterialOverride(doc, ds.Id, materialCache[typeId]);
                                }

                                createdPanels.Add(new
                                {
                                    ElementId = ds.Id.GetIntValue(),
                                    Name = dsName,
                                    Floor = floor + 1,
                                    Column = col + 1,
                                    TypeId = typeId
                                });

                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                failedPanels.Add(new
                                {
                                    Floor = floor + 1,
                                    Column = col + 1,
                                    TypeId = typeId,
                                    Reason = ex.Message
                                });
                                failCount++;
                            }

                            x += pw + gap;
                        }
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully created {successCount} facade panels, {failCount} failed",
                    Response = new
                    {
                        SuccessCount = successCount,
                        FailCount = failCount,
                        CreatedPanels = createdPanels,
                        FailedPanels = failedPanels
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error creating facade from analysis: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "CreateFacadeFromAnalysisEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
