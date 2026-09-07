using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class CreateDirectShapeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string Name { get; set; }
        public string Category { get; set; } = "OST_GenericModel";
        public string ShapeType { get; set; } = "box"; // "box" or "polygon_extrusion"
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<Tuple<double, double>> PolygonPoints { get; set; }

        public AIResult<object> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Create DirectShape";

        private ElementId ResolveCategoryId(Document doc, string catName)
        {
            if (Enum.TryParse<BuiltInCategory>(catName, true, out var bic))
            {
                return new ElementId(bic);
            }
            if (!catName.StartsWith("OST_") && Enum.TryParse<BuiltInCategory>("OST_" + catName, true, out bic))
            {
                return new ElementId(bic);
            }
            return new ElementId(BuiltInCategory.OST_GenericModel);
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<object> { Success = false, Message = "No active Revit document." };
                    return;
                }

                ElementId catId = ResolveCategoryId(doc, Category);
                Solid solid = null;

                double xFt = X / 304.8;
                double yFt = Y / 304.8;
                double zFt = Z / 304.8;
                double hFt = (Height > 0 ? Height : 1000) / 304.8;

                if (ShapeType.Equals("polygon_extrusion", StringComparison.OrdinalIgnoreCase) && PolygonPoints != null && PolygonPoints.Count >= 3)
                {
                    CurveLoop loop = new CurveLoop();
                    for (int i = 0; i < PolygonPoints.Count; i++)
                    {
                        var p1 = PolygonPoints[i];
                        var p2 = PolygonPoints[(i + 1) % PolygonPoints.Count];
                        XYZ pt1 = new XYZ(xFt + p1.Item1 / 304.8, yFt + p1.Item2 / 304.8, zFt);
                        XYZ pt2 = new XYZ(xFt + p2.Item1 / 304.8, yFt + p2.Item2 / 304.8, zFt);
                        loop.Append(Line.CreateBound(pt1, pt2));
                    }
                    solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, hFt);
                }
                else
                {
                    // Default Box
                    double lFt = (Length > 0 ? Length : 2000) / 304.8;
                    double wFt = (Width > 0 ? Width : 1000) / 304.8;

                    CurveLoop loop = new CurveLoop();
                    XYZ p0 = new XYZ(xFt, yFt, zFt);
                    XYZ p1 = new XYZ(xFt + lFt, yFt, zFt);
                    XYZ p2 = new XYZ(xFt + lFt, yFt + wFt, zFt);
                    XYZ p3 = new XYZ(xFt, yFt + wFt, zFt);

                    loop.Append(Line.CreateBound(p0, p1));
                    loop.Append(Line.CreateBound(p1, p2));
                    loop.Append(Line.CreateBound(p2, p3));
                    loop.Append(Line.CreateBound(p3, p0));

                    solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, hFt);
                }

                if (solid == null)
                {
                    Result = new AIResult<object> { Success = false, Message = "Failed to construct 3D solid geometry." };
                    return;
                }

                DirectShape directShape = null;
                TransactionUtils.ExecuteInTransaction(doc, $"Create DirectShape {Name}", () =>
                {
                    directShape = DirectShape.CreateElement(doc, catId);
                    directShape.SetShape(new GeometryObject[] { solid });
                    if (!string.IsNullOrEmpty(Name))
                    {
                        directShape.Name = Name;
                    }
                });

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully created 3D DirectShape '{Name ?? "DirectShape"}' (ID: {directShape.Id.GetValue()}).",
                    Response = new
                    {
                        ElementId = directShape.Id.GetValue(),
                        Name = directShape.Name,
                        Category = directShape.Category?.Name ?? Category
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error creating DirectShape: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
