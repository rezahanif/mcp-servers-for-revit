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

namespace RevitMCPCommandSet.Services.Views
{
    public class AdjustSectionDatumsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                JArray viewIdsArray = _parameters?["viewIds"] as JArray;
                if (viewIdsArray == null || viewIdsArray.Count == 0)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = "Parameter 'viewIds' is missing or empty."
                    };
                    return;
                }

                var processedViews = new List<string>();
                var errors = new List<string>();

                using (Transaction trans = new Transaction(doc, "Adjust Section Datums"))
                {
                    trans.Start();

                    foreach (var token in viewIdsArray)
                    {
                        try
                        {
                            long val = token.Value<long>();
                            ElementId id = val.ToElementId();
                            Element elem = doc.GetElement(id);
                            if (elem == null)
                            {
                                errors.Add($"Element ID {val} not found.");
                                continue;
                            }

                            View view = elem as View;
                            if (view == null)
                            {
                                string name = elem.Name;
                                view = new FilteredElementCollector(doc)
                                    .OfClass(typeof(View))
                                    .Cast<View>()
                                    .FirstOrDefault(v => v.Name == name && v.ViewType == ViewType.Section);
                            }

                            if (view == null)
                            {
                                errors.Add($"Element ID {val} could not be resolved to a section view.");
                                continue;
                            }

                            BoundingBoxXYZ cropBox = EnsureAndGetCropBox(view);
                            if (cropBox == null)
                            {
                                errors.Add($"View {view.Name} cannot get CropBox bounds.");
                                continue;
                            }

                            double minX = cropBox.Min.X * 304.8;
                            double maxX = cropBox.Max.X * 304.8;
                            double minY = cropBox.Min.Y * 304.8;
                            double maxY = cropBox.Max.Y * 304.8;

                            AdjustGridsInView(doc, view, cropBox, GetViewTransform(view));
                            AdjustLevelsInView(doc, view, cropBox, GetViewTransform(view));

                            processedViews.Add($"{view.Name} (Crop Box X: {minX:F0} ~ {maxX:F0}, Y: {minY:F0} ~ {maxY:F0} mm)");
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Error processing view: {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = errors.Count == 0,
                    Message = $"Successfully adjusted section datums for {processedViews.Count} views",
                    Response = new
                    {
                        ProcessedCount = processedViews.Count,
                        ProcessedViews = processedViews,
                        Errors = errors
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error adjusting section datums: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private Transform GetViewTransform(View view)
        {
            Transform transform = Transform.Identity;
            transform.BasisX = view.RightDirection;
            transform.BasisY = view.UpDirection;
            transform.BasisZ = view.ViewDirection;
            transform.Origin = view.Origin;
            return transform;
        }

        private BoundingBoxXYZ EnsureAndGetCropBox(View view)
        {
            if (view == null) return null;

            if (!view.CropBoxActive)
            {
                view.CropBoxActive = true;
                view.CropBoxVisible = true;
            }

            return view.CropBox;
        }

        private void AdjustGridsInView(Document doc, View view, BoundingBoxXYZ cropBox, Transform transform)
        {
            var grids = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            double offsetFeet = 150.0 / 304.8;
            Transform invTrans = transform.Inverse;

            foreach (var grid in grids)
            {
                try
                {
                    grid.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                    grid.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);

                    IList<Curve> curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                    if (curves == null || curves.Count == 0) continue;

                    Curve curve = curves[0];
                    XYZ gP0_world = curve.GetEndPoint(0);
                    XYZ gP1_world = curve.GetEndPoint(1);

                    XYZ gP0_local = invTrans.OfPoint(gP0_world);
                    XYZ gP1_local = invTrans.OfPoint(gP1_world);

                    bool p0IsTop = gP0_local.Y > gP1_local.Y;
                    XYZ top_local = p0IsTop ? gP0_local : gP1_local;
                    XYZ bottom_local = p0IsTop ? gP1_local : gP0_local;

                    XYZ newTop_local = new XYZ(top_local.X, cropBox.Max.Y + offsetFeet, top_local.Z);
                    XYZ newBottom_local = new XYZ(bottom_local.X, cropBox.Min.Y - offsetFeet, bottom_local.Z);

                    XYZ newTop_world = transform.OfPoint(newTop_local);
                    XYZ newBottom_world = transform.OfPoint(newBottom_local);

                    Line newCurve = Line.CreateBound(newBottom_world, newTop_world);
                    grid.SetCurveInView(DatumExtentType.ViewSpecific, view, newCurve);

                    grid.HideBubbleInView(DatumEnds.End0, view);
                    grid.ShowBubbleInView(DatumEnds.End1, view);
                }
                catch
                {
                }
            }
        }

        private void AdjustLevelsInView(Document doc, View view, BoundingBoxXYZ cropBox, Transform transform)
        {
            var levels = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            Transform invTrans = transform.Inverse;
            double viewScale = view.Scale;

            double basePaperWidth = 10.0;
            double charPaperWidth = 2.2;

            foreach (var level in levels)
            {
                try
                {
                    level.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                    level.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);

                    IList<Curve> curves = level.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                    if (curves == null || curves.Count == 0) continue;

                    Curve curve = curves[0];
                    XYZ lP0_world = curve.GetEndPoint(0);
                    XYZ lP1_world = curve.GetEndPoint(1);

                    XYZ lP0_local = invTrans.OfPoint(lP0_world);
                    XYZ lP1_local = invTrans.OfPoint(lP1_world);

                    bool p0IsLeft = lP0_local.X < lP1_local.X;
                    XYZ left_local = p0IsLeft ? lP0_local : lP1_local;
                    XYZ right_local = p0IsLeft ? lP1_local : lP0_local;

                    string levelName = level.Name ?? "";
                    double paperWidth = basePaperWidth + (levelName.Length * charPaperWidth);
                    double offsetFeet = (paperWidth * viewScale) / 304.8;

                    XYZ newLeft_local = new XYZ(cropBox.Min.X - offsetFeet, left_local.Y, left_local.Z);
                    XYZ newRight_local = new XYZ(cropBox.Max.X + offsetFeet, right_local.Y, right_local.Z);

                    XYZ newLeft_world = transform.OfPoint(newLeft_local);
                    XYZ newRight_world = transform.OfPoint(newRight_local);

                    Line newCurve = Line.CreateBound(newLeft_world, newRight_world);
                    level.SetCurveInView(DatumExtentType.ViewSpecific, view, newCurve);

                    level.ShowBubbleInView(DatumEnds.End0, view);
                    level.ShowBubbleInView(DatumEnds.End1, view);
                }
                catch
                {
                }
            }
        }

        public string GetName() => "AdjustSectionDatumsEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 20000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
