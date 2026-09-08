using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Services.Sheets
{
    /// <summary>
    /// Shared static helpers for sheet/viewport/titleblock geometry tools.
    /// Ported from REVIT_MCP_study's CommandExecutor.ViewportPosition.cs /
    /// CommandExecutor.TitleblockAlign.cs / CommandExecutor.Sheet.cs (private
    /// helper methods there), made public static here so multiple
    /// EventHandlers in this domain can share them without duplication.
    /// All coordinates in/out of these helpers are Revit internal units (feet)
    /// unless a parameter name says "Mm".
    /// </summary>
    public static class SheetGeometryHelpers
    {
        /// <summary>
        /// Resolve target viewports by priority: viewportIds > viewIds > viewNames > viewNameContains,
        /// further filtered by sheetNumbers and viewTypeFilter if given.
        /// </summary>
        public static List<Viewport> ResolveTargetViewports(
            Document doc,
            List<int> viewportIds,
            List<int> viewIds,
            List<string> viewNames,
            string viewNameContains,
            List<string> sheetNumbers,
            List<string> viewTypeFilter)
        {
            List<Viewport> viewports;

            if (viewportIds != null && viewportIds.Count > 0)
            {
                viewports = viewportIds
                    .Select(id => doc.GetElement(new ElementId(id)) as Viewport)
                    .Where(v => v != null)
                    .ToList();
            }
            else
            {
                var allViewports = new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                viewports = allViewports;

                if (viewIds != null && viewIds.Count > 0)
                {
                    var viewIdSet = new HashSet<int>(viewIds);
                    viewports = viewports.Where(vp => viewIdSet.Contains(vp.ViewId.GetIntValue())).ToList();
                }
                else if (viewNames != null && viewNames.Count > 0)
                {
                    var nameSet = new HashSet<string>(viewNames);
                    viewports = viewports.Where(vp =>
                    {
                        var view = doc.GetElement(vp.ViewId) as View;
                        return view != null && nameSet.Contains(view.Name);
                    }).ToList();
                }
                else if (!string.IsNullOrEmpty(viewNameContains))
                {
                    viewports = viewports.Where(vp =>
                    {
                        var view = doc.GetElement(vp.ViewId) as View;
                        return view != null && view.Name.IndexOf(viewNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
                    }).ToList();
                }
                else if (sheetNumbers == null || sheetNumbers.Count == 0)
                {
                    throw new Exception("Must specify one of viewportIds / viewIds / viewNames / viewNameContains / sheetNumbers");
                }
            }

            if (sheetNumbers != null && sheetNumbers.Count > 0)
            {
                var sheetNumSet = new HashSet<string>(sheetNumbers);
                viewports = viewports.Where(vp =>
                {
                    var sheet = doc.GetElement(vp.SheetId) as ViewSheet;
                    return sheet != null && sheetNumSet.Contains(sheet.SheetNumber);
                }).ToList();
            }

            if (viewTypeFilter != null && viewTypeFilter.Count > 0)
            {
                var typeSet = new HashSet<string>(viewTypeFilter, StringComparer.OrdinalIgnoreCase);
                viewports = viewports.Where(vp =>
                {
                    var view = doc.GetElement(vp.ViewId) as View;
                    return view != null && typeSet.Contains(view.ViewType.ToString());
                }).ToList();
            }

            return viewports;
        }

        /// <summary>Sheet-space reference point (feet, Y+ up) for "titleblock-*" / "sheet-*" reference tokens.</summary>
        public static XYZ GetSheetReferencePoint(Document doc, ViewSheet sheet, string reference)
        {
            if (reference.StartsWith("titleblock-"))
            {
                var tb = new FilteredElementCollector(doc, sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .FirstOrDefault();
                if (tb == null) return null;

                BoundingBoxXYZ bbox = tb.get_BoundingBox(sheet);
                if (bbox == null) return null;

                switch (reference)
                {
                    case "titleblock-top-left": return new XYZ(bbox.Min.X, bbox.Max.Y, 0);
                    case "titleblock-top-right": return new XYZ(bbox.Max.X, bbox.Max.Y, 0);
                    case "titleblock-bottom-left": return new XYZ(bbox.Min.X, bbox.Min.Y, 0);
                    case "titleblock-bottom-right": return new XYZ(bbox.Max.X, bbox.Min.Y, 0);
                    default: return null;
                }
            }
            else if (reference.StartsWith("sheet-"))
            {
                BoundingBoxUV outline = sheet.Outline;
                switch (reference)
                {
                    case "sheet-top-left": return new XYZ(outline.Min.U, outline.Max.V, 0);
                    case "sheet-top-right": return new XYZ(outline.Max.U, outline.Max.V, 0);
                    case "sheet-bottom-left": return new XYZ(outline.Min.U, outline.Min.V, 0);
                    case "sheet-bottom-right": return new XYZ(outline.Max.U, outline.Min.V, 0);
                    default: return null;
                }
            }
            return null;
        }

        /// <summary>Offset of an anchor corner ("top-left" etc.) relative to a given center point.</summary>
        public static XYZ GetAnchorOffsetFromCenter(string anchor, XYZ outlineMin, XYZ outlineMax, XYZ center)
        {
            XYZ anchorPos;
            switch (anchor)
            {
                case "top-left": anchorPos = new XYZ(outlineMin.X, outlineMax.Y, 0); break;
                case "top-right": anchorPos = new XYZ(outlineMax.X, outlineMax.Y, 0); break;
                case "bottom-left": anchorPos = new XYZ(outlineMin.X, outlineMin.Y, 0); break;
                case "bottom-right": anchorPos = new XYZ(outlineMax.X, outlineMin.Y, 0); break;
                case "center": anchorPos = center; break;
                default: throw new Exception($"Unsupported viewAnchor: '{anchor}' (allowed: top-left/top-right/bottom-left/bottom-right/center)");
            }
            return new XYZ(anchorPos.X - center.X, anchorPos.Y - center.Y, 0);
        }

        public static ViewSheet FindSheetByNumber(Document doc, string sheetNumber)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Sheets)
                .Cast<ViewSheet>()
                .FirstOrDefault(s => s.SheetNumber == sheetNumber);
        }

        public static Element GetFirstTitleblock(Document doc, ViewSheet sheet)
        {
            return new FilteredElementCollector(doc, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .FirstOrDefault();
        }

        public static XYZ GetTitleblockAnchorPoint(Element titleblock, ViewSheet sheet, string anchor)
        {
            BoundingBoxXYZ bbox = titleblock.get_BoundingBox(sheet);
            if (bbox == null) return null;

            switch (anchor)
            {
                case "top-left": return new XYZ(bbox.Min.X, bbox.Max.Y, 0);
                case "top-right": return new XYZ(bbox.Max.X, bbox.Max.Y, 0);
                case "bottom-left": return new XYZ(bbox.Min.X, bbox.Min.Y, 0);
                case "bottom-right": return new XYZ(bbox.Max.X, bbox.Min.Y, 0);
                case "center":
                    return new XYZ((bbox.Min.X + bbox.Max.X) / 2.0, (bbox.Min.Y + bbox.Max.Y) / 2.0, 0);
                default: return null;
            }
        }

        /// <summary>Increment a trailing numeric suffix in a string (e.g. "A101" -> "A102").</summary>
        public static string IncrementString(string input)
        {
            var match = System.Text.RegularExpressions.Regex.Match(input, @"(.*?)([0-9]+)$");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                string numberStr = match.Groups[2].Value;
                long number = long.Parse(numberStr) + 1;
                return prefix + number.ToString().PadLeft(numberStr.Length, '0');
            }
            return input + "-1";
        }
    }
}
