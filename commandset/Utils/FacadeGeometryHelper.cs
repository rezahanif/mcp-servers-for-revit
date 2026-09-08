using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Utils
{
    public static class FacadeGeometryHelper
    {
        public static Solid CreateCurvedPanelSolid(
            XYZ wallStart, XYZ wallDir, XYZ wallNormal,
            double posAlongMm, double posZMm, double widthMm, double heightMm,
            double depthMm, double thicknessMm, string curveType, double offsetMm)
        {
            double w = widthMm / 304.8;
            double h = heightMm / 304.8;
            double d = depthMm / 304.8;
            double t = thicknessMm / 304.8;
            double off = offsetMm / 304.8;
            double posA = posAlongMm / 304.8;
            double posZ = posZMm / 304.8;

            XYZ center = wallStart + wallDir * posA + wallNormal * off;
            XYZ p1 = center - wallDir * (w / 2);
            XYZ p2 = center + wallDir * (w / 2);

            double arcSign = curveType == "concave" ? 1.0 : -1.0;
            XYZ midPt = center + wallNormal * (d * arcSign);

            p1 = new XYZ(p1.X, p1.Y, posZ);
            p2 = new XYZ(p2.X, p2.Y, posZ);
            midPt = new XYZ(midPt.X, midPt.Y, posZ);

            Arc innerArc = Arc.Create(p1, p2, midPt);
            XYZ p1o = p1 + wallNormal * (t * arcSign);
            XYZ p2o = p2 + wallNormal * (t * arcSign);
            XYZ midO = midPt + wallNormal * (t * arcSign);
            Arc outerArc = Arc.Create(p1o, p2o, midO);

            CurveLoop profile = new CurveLoop();
            profile.Append(innerArc);
            profile.Append(Line.CreateBound(p2, p2o));
            profile.Append(outerArc.CreateReversed());
            profile.Append(Line.CreateBound(p1o, p1));

            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { profile }, XYZ.BasisZ, h);
        }

        public static Solid CreateBeveledOpeningSolid(
            XYZ wallStart, XYZ wallDir, XYZ wallNormal,
            double posAlongMm, double posZMm, double frameWidthMm, double frameHeightMm,
            double openWidthMm, double openHeightMm, double bevelDepthMm, double frameThickMm,
            string bevelDirection, double offsetMm)
        {
            double fw = frameWidthMm / 304.8;
            double fh = frameHeightMm / 304.8;
            double ow = openWidthMm / 304.8;
            double oh = openHeightMm / 304.8;
            double bd = bevelDepthMm / 304.8;
            double ft = frameThickMm / 304.8;
            double off = offsetMm / 304.8;
            double posA = posAlongMm / 304.8;
            double posZ = posZMm / 304.8;

            XYZ center = wallStart + wallDir * posA + wallNormal * off;

            XYZ oA = center - wallDir * (fw / 2) + new XYZ(0, 0, posZ);
            XYZ oB = center + wallDir * (fw / 2) + new XYZ(0, 0, posZ);
            XYZ oC = center + wallDir * (fw / 2) + new XYZ(0, 0, posZ + fh);
            XYZ oD = center - wallDir * (fw / 2) + new XYZ(0, 0, posZ + fh);

            XYZ innerCenter = center + wallNormal * bd;

            XYZ iA = innerCenter - wallDir * (ow / 2) + new XYZ(0, 0, posZ + (fh - oh) / 2);
            XYZ iB = innerCenter + wallDir * (ow / 2) + new XYZ(0, 0, posZ + (fh - oh) / 2);
            XYZ iC = innerCenter + wallDir * (ow / 2) + new XYZ(0, 0, posZ + (fh + oh) / 2);
            XYZ iD = innerCenter - wallDir * (ow / 2) + new XYZ(0, 0, posZ + (fh + oh) / 2);

            XYZ dirShift = XYZ.Zero;
            switch (bevelDirection?.ToLowerInvariant())
            {
                case "up": dirShift = new XYZ(0, 0, (fh - oh) * 0.15); break;
                case "down": dirShift = new XYZ(0, 0, -(fh - oh) * 0.15); break;
                case "left": dirShift = -wallDir * (fw - ow) * 0.15; break;
                case "right": dirShift = wallDir * (fw - ow) * 0.15; break;
            }
            iA += dirShift;
            iB += dirShift;
            iC += dirShift;
            iD += dirShift;

            CurveLoop outerProfile = new CurveLoop();
            outerProfile.Append(Line.CreateBound(oA, oB));
            outerProfile.Append(Line.CreateBound(oB, oC));
            outerProfile.Append(Line.CreateBound(oC, oD));
            outerProfile.Append(Line.CreateBound(oD, oA));

            Solid outerBox = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { outerProfile }, wallNormal, bd + ft);

            CurveLoop innerProfile = new CurveLoop();
            innerProfile.Append(Line.CreateBound(iA, iB));
            innerProfile.Append(Line.CreateBound(iB, iC));
            innerProfile.Append(Line.CreateBound(iC, iD));
            innerProfile.Append(Line.CreateBound(iD, iA));

            Solid innerVoid = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { innerProfile }, wallNormal, ft + 0.01);

            return BooleanOperationsUtils.ExecuteBooleanOperation(
                outerBox, innerVoid, BooleanOperationsType.Difference);
        }

        public static Solid CreateFlatPanelSolid(
            XYZ wallStart, XYZ wallDir, XYZ wallNormal,
            double posAlongMm, double posZMm, double widthMm, double heightMm,
            double thicknessMm, double offsetMm)
        {
            double w = widthMm / 304.8;
            double h = heightMm / 304.8;
            double t = thicknessMm / 304.8;
            double off = offsetMm / 304.8;
            double posA = posAlongMm / 304.8;
            double posZ = posZMm / 304.8;

            XYZ center = wallStart + wallDir * posA + wallNormal * off;
            XYZ p1 = center - wallDir * (w / 2) + new XYZ(0, 0, posZ);
            XYZ p2 = center + wallDir * (w / 2) + new XYZ(0, 0, posZ);
            XYZ p3 = center + wallDir * (w / 2) + new XYZ(0, 0, posZ + h);
            XYZ p4 = center - wallDir * (w / 2) + new XYZ(0, 0, posZ + h);

            CurveLoop profile = new CurveLoop();
            profile.Append(Line.CreateBound(p1, p2));
            profile.Append(Line.CreateBound(p2, p3));
            profile.Append(Line.CreateBound(p3, p4));
            profile.Append(Line.CreateBound(p4, p1));

            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { profile }, wallNormal, t);
        }

        public static Solid CreateAngledPanelSolid(
            XYZ wallStart, XYZ wallDir, XYZ wallNormal,
            double posAlongMm, double posZMm, double widthMm, double heightMm,
            double thicknessMm, double offsetMm, double tiltAngleDeg, string tiltAxis)
        {
            double w = widthMm / 304.8;
            double h = heightMm / 304.8;
            double t = thicknessMm / 304.8;
            double off = offsetMm / 304.8;
            double posA = posAlongMm / 304.8;
            double posZ = posZMm / 304.8;
            double angleRad = tiltAngleDeg * Math.PI / 180.0;

            XYZ center = wallStart + wallDir * posA + wallNormal * off;

            XYZ p1, p2, p3, p4;
            if (tiltAxis == "horizontal")
            {
                double topOffset = Math.Tan(angleRad) * h / 2;
                p1 = new XYZ(-w / 2, -topOffset, 0);
                p2 = new XYZ(w / 2, -topOffset, 0);
                p3 = new XYZ(w / 2, topOffset, h);
                p4 = new XYZ(-w / 2, topOffset, h);
            }
            else
            {
                double sideOffset = Math.Tan(angleRad) * w / 2;
                p1 = new XYZ(-w / 2, -sideOffset, 0);
                p2 = new XYZ(w / 2, sideOffset, 0);
                p3 = new XYZ(w / 2, sideOffset, h);
                p4 = new XYZ(-w / 2, -sideOffset, h);
            }

            Transform localToWorld = Transform.Identity;
            localToWorld.BasisX = wallDir;
            localToWorld.BasisY = wallNormal;
            localToWorld.BasisZ = XYZ.BasisZ;
            localToWorld.Origin = center + new XYZ(0, 0, posZ);

            XYZ wp1 = localToWorld.OfPoint(p1);
            XYZ wp2 = localToWorld.OfPoint(p2);
            XYZ wp3 = localToWorld.OfPoint(p3);
            XYZ wp4 = localToWorld.OfPoint(p4);

            CurveLoop frontProfile = new CurveLoop();
            frontProfile.Append(Line.CreateBound(wp1, wp2));
            frontProfile.Append(Line.CreateBound(wp2, wp3));
            frontProfile.Append(Line.CreateBound(wp3, wp4));
            frontProfile.Append(Line.CreateBound(wp4, wp1));

            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { frontProfile }, wallNormal, t);
        }

        public static Solid CreateRoundedOpeningSolid(
            XYZ wallStart, XYZ wallDir, XYZ wallNormal,
            double posAlongMm, double posZMm, double frameWidthMm, double frameHeightMm,
            double openWidthMm, double openHeightMm, double depthMm, double thicknessMm,
            double cornerRadiusMm, string openingShape, double offsetMm)
        {
            double fw = frameWidthMm / 304.8;
            double fh = frameHeightMm / 304.8;
            double ow = openWidthMm / 304.8;
            double oh = openHeightMm / 304.8;
            double dep = depthMm / 304.8;
            double ft = thicknessMm / 304.8;
            double cr = cornerRadiusMm / 304.8;
            double off = offsetMm / 304.8;
            double posA = posAlongMm / 304.8;
            double posZ = posZMm / 304.8;

            cr = Math.Min(cr, Math.Min(ow / 2, oh / 2));

            XYZ center = wallStart + wallDir * posA + wallNormal * off;

            XYZ oA = center - wallDir * (fw / 2) + new XYZ(0, 0, posZ);
            XYZ oB = center + wallDir * (fw / 2) + new XYZ(0, 0, posZ);
            XYZ oC = center + wallDir * (fw / 2) + new XYZ(0, 0, posZ + fh);
            XYZ oD = center - wallDir * (fw / 2) + new XYZ(0, 0, posZ + fh);

            CurveLoop outerProfile = new CurveLoop();
            outerProfile.Append(Line.CreateBound(oA, oB));
            outerProfile.Append(Line.CreateBound(oB, oC));
            outerProfile.Append(Line.CreateBound(oC, oD));
            outerProfile.Append(Line.CreateBound(oD, oA));

            Solid outerBox = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { outerProfile }, wallNormal, dep + ft);

            XYZ iCenter = center + wallNormal * ft;
            double iLeft = -ow / 2;
            double iRight = ow / 2;
            double iBottom = posZ + (fh - oh) / 2;
            double iTop = posZ + (fh + oh) / 2;

            CurveLoop innerProfile = CreateRoundedRectProfile(
                iCenter, wallDir, iLeft, iRight, iBottom, iTop, cr, openingShape);

            Solid innerVoid = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { innerProfile }, wallNormal, dep + 0.01);

            return BooleanOperationsUtils.ExecuteBooleanOperation(
                outerBox, innerVoid, BooleanOperationsType.Difference);
        }

        private static CurveLoop CreateRoundedRectProfile(
            XYZ center, XYZ wallDir,
            double left, double right, double bottom, double top,
            double radius, string shape)
        {
            CurveLoop loop = new CurveLoop();

            XYZ pBL = center + wallDir * left + new XYZ(0, 0, bottom);
            XYZ pBR = center + wallDir * right + new XYZ(0, 0, bottom);
            XYZ pTR = center + wallDir * right + new XYZ(0, 0, top);
            XYZ pTL = center + wallDir * left + new XYZ(0, 0, top);

            if (radius <= 0.001 || shape == "rect")
            {
                loop.Append(Line.CreateBound(pBL, pBR));
                loop.Append(Line.CreateBound(pBR, pTR));
                loop.Append(Line.CreateBound(pTR, pTL));
                loop.Append(Line.CreateBound(pTL, pBL));
                return loop;
            }

            if (shape == "arch")
            {
                double archRadius = (right - left) / 2;
                double archCenterZ = top - archRadius;

                loop.Append(Line.CreateBound(pBL, pBR));
                XYZ archStartR = center + wallDir * right + new XYZ(0, 0, archCenterZ);
                loop.Append(Line.CreateBound(pBR, archStartR));
                XYZ archTop = center + new XYZ(0, 0, top);
                XYZ archStartL = center + wallDir * left + new XYZ(0, 0, archCenterZ);
                Arc arch = Arc.Create(archStartR, archStartL, archTop);
                loop.Append(arch);
                loop.Append(Line.CreateBound(archStartL, pBL));
                return loop;
            }

            double r = radius;
            XYZ pBL_r = pBL + wallDir * r;
            XYZ pBR_l = pBR - wallDir * r;
            XYZ pBR_u = pBR + new XYZ(0, 0, r);
            XYZ pTR_d = pTR - new XYZ(0, 0, r);
            XYZ pTR_l = pTR - wallDir * r;
            XYZ pTL_r = pTL + wallDir * r;
            XYZ pTL_d = pTL - new XYZ(0, 0, r);
            XYZ pBL_u = pBL + new XYZ(0, 0, r);

            loop.Append(Line.CreateBound(pBL_r, pBR_l));
            loop.Append(Arc.Create(pBR_l, pBR_u, pBR + (-wallDir + new XYZ(0, 0, 1)) * (r * 0.293)));
            loop.Append(Line.CreateBound(pBR_u, pTR_d));
            loop.Append(Arc.Create(pTR_d, pTR_l, pTR + (-wallDir - new XYZ(0, 0, 1)) * (r * 0.293)));
            loop.Append(Line.CreateBound(pTR_l, pTL_r));
            loop.Append(Arc.Create(pTL_r, pTL_d, pTL + (wallDir - new XYZ(0, 0, 1)) * (r * 0.293)));
            loop.Append(Line.CreateBound(pTL_d, pBL_u));
            loop.Append(Arc.Create(pBL_u, pBL_r, pBL + (wallDir + new XYZ(0, 0, 1)) * (r * 0.293)));

            return loop;
        }

        public static Material FindOrCreateFacadeMaterial(Document doc, string colorHex, string baseName)
        {
            colorHex = colorHex.TrimStart('#');
            byte r = 128, g = 128, b = 128;
            if (colorHex.Length >= 6)
            {
                r = Convert.ToByte(colorHex.Substring(0, 2), 16);
                g = Convert.ToByte(colorHex.Substring(2, 2), 16);
                b = Convert.ToByte(colorHex.Substring(4, 2), 16);
            }

            string materialName = $"FP_MAT_{baseName}";

            Material material = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name == materialName);

            if (material == null)
            {
                ElementId newMatId = Material.Create(doc, materialName);
                material = doc.GetElement(newMatId) as Material;
            }

            if (material != null)
            {
                material.Color = new Color(r, g, b);
                material.Transparency = 0;
            }

            return material;
        }

        public static void ApplyMaterialOverride(Document doc, ElementId elementId, Material mat)
        {
            View activeView = doc.ActiveView;
            if (activeView == null || mat == null) return;

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternColor(mat.Color);

            FillPatternElement solidFill = FillPatternElement.GetFillPatternElementByName(
                doc, FillPatternTarget.Drafting, "<Solid fill>");
            if (solidFill == null)
            {
                solidFill = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);
            }
            if (solidFill != null)
                ogs.SetSurfaceForegroundPatternId(solidFill.Id);

            activeView.SetElementOverrides(elementId, ogs);
        }
    }
}
