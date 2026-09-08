using System;
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
    public class CreateFacadePanelEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                double positionAlongWall = _parameters["positionAlongWall"]?.Value<double>() ?? 0;
                double positionZ = _parameters["positionZ"]?.Value<double>() ?? 0;
                double width = _parameters["width"]?.Value<double>() ?? 800;
                double height = _parameters["height"]?.Value<double>() ?? 3400;
                double depth = _parameters["depth"]?.Value<double>() ?? 150;
                double thickness = _parameters["thickness"]?.Value<double>() ?? 30;
                double offset = _parameters["offset"]?.Value<double>() ?? 200;
                string colorHex = _parameters["color"]?.Value<string>() ?? "#B85C3A";
                string panelName = _parameters["name"]?.Value<string>() ?? "FacadePanel";
                string geometryType = _parameters["geometryType"]?.Value<string>() ?? "curved_panel";

                string curveType = _parameters["curveType"]?.Value<string>() ?? "concave";
                string bevelDirection = _parameters["bevelDirection"]?.Value<string>() ?? "center";
                double openingWidth = _parameters["openingWidth"]?.Value<double>() ?? 600;
                double openingHeight = _parameters["openingHeight"]?.Value<double>() ?? 800;
                double bevelDepth = _parameters["bevelDepth"]?.Value<double>() ?? 300;
                double tiltAngle = _parameters["tiltAngle"]?.Value<double>() ?? 15;
                string tiltAxis = _parameters["tiltAxis"]?.Value<string>() ?? "horizontal";
                double cornerRadius = _parameters["cornerRadius"]?.Value<double>() ?? 100;
                string openingShape = _parameters["openingShape"]?.Value<string>() ?? "rounded_rect";

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

                if (wall == null) throw new InvalidOperationException("Reference wall not found");

                LocationCurve wallLoc = wall.Location as LocationCurve;
                if (wallLoc == null) throw new InvalidOperationException("Cannot get wall location curve");
                Line wallLine = wallLoc.Curve as Line;
                if (wallLine == null) throw new InvalidOperationException("Only straight walls are supported");

                XYZ wallDir = wallLine.Direction.Normalize();
                XYZ wallNormal = wall.Orientation.Normalize();
                double halfWallThickness = wall.Width / 2.0;
                XYZ wallExteriorStart = wallLine.GetEndPoint(0) + wallNormal * halfWallThickness;

                using (Transaction trans = new Transaction(doc, $"Create Facade Panel: {panelName}"))
                {
                    trans.Start();

                    Solid solid;
                    switch (geometryType)
                    {
                        case "beveled_opening":
                            solid = FacadeGeometryHelper.CreateBeveledOpeningSolid(
                                wallExteriorStart, wallDir, wallNormal,
                                positionAlongWall, positionZ, width, height,
                                openingWidth, openingHeight, bevelDepth, thickness,
                                bevelDirection, offset);
                            break;
                        case "angled_panel":
                            solid = FacadeGeometryHelper.CreateAngledPanelSolid(
                                wallExteriorStart, wallDir, wallNormal,
                                positionAlongWall, positionZ, width, height,
                                thickness, offset, tiltAngle, tiltAxis);
                            break;
                        case "rounded_opening":
                            solid = FacadeGeometryHelper.CreateRoundedOpeningSolid(
                                wallExteriorStart, wallDir, wallNormal,
                                positionAlongWall, positionZ, width, height,
                                openingWidth, openingHeight, depth, thickness,
                                cornerRadius, openingShape, offset);
                            break;
                        case "flat_panel":
                            solid = FacadeGeometryHelper.CreateFlatPanelSolid(
                                wallExteriorStart, wallDir, wallNormal,
                                positionAlongWall, positionZ, width, height,
                                thickness, offset);
                            break;
                        case "curved_panel":
                        default:
                            solid = FacadeGeometryHelper.CreateCurvedPanelSolid(
                                wallExteriorStart, wallDir, wallNormal,
                                positionAlongWall, positionZ, width, height,
                                depth, thickness, curveType, offset);
                            break;
                    }

#if REVIT2024_OR_GREATER
                    DirectShape ds = DirectShape.CreateElement(doc, new ElementId((long)BuiltInCategory.OST_GenericModel));
#else
                    DirectShape ds = DirectShape.CreateElement(doc, new ElementId((int)BuiltInCategory.OST_GenericModel));
#endif
                    ds.ApplicationId = "RevitMCP_FacadePanel";
                    ds.ApplicationDataId = panelName;
                    ds.SetShape(new GeometryObject[] { solid });

                    Material mat = FacadeGeometryHelper.FindOrCreateFacadeMaterial(doc, colorHex, panelName);
                    FacadeGeometryHelper.ApplyMaterialOverride(doc, ds.Id, mat);

                    trans.Commit();

                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"Successfully created facade panel: {panelName} ({geometryType})",
                        Response = new
                        {
                            ElementId = ds.Id.GetIntValue(),
                            Name = panelName,
                            GeometryType = geometryType,
                            Width = width,
                            Height = height,
                            Depth = depth,
                            Color = colorHex
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error creating facade panel: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "CreateFacadePanelEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
