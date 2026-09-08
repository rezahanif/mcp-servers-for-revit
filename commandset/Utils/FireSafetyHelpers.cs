using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>
    /// Shared helpers for the FireSafety domain (smoke exhaust, smoke detector,
    /// parallel-section, stair-compliance tools). Ported from REVIT_MCP_study's
    /// CommandExecutor.cs (FindLevel, GetParamValue) so each EventHandler in this
    /// domain does not duplicate the same lookup logic.
    /// </summary>
    public static class FireSafetyHelpers
    {
        public const double FeetToMm = 304.8;
        public const double MmToFeet = 1.0 / 304.8;
        public const double SqFeetToSqM = 0.092903;

        /// <summary>Find a level by exact or partial name match; falls back to the lowest level.</summary>
        public static Level FindLevel(Document doc, string levelName, bool useFirstIfNotFound = true)
        {
            Level level = null;
            if (!string.IsNullOrEmpty(levelName))
            {
                level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault(l => l.Name == levelName || l.Name.Contains(levelName) || levelName.Contains(l.Name));
            }

            if (level == null && useFirstIfNotFound)
            {
                level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .FirstOrDefault();
            }

            if (level == null)
                throw new System.Exception($"Level not found: {levelName}");

            return level;
        }

        private static double? GetParamDouble(Element e, BuiltInParameter bip)
        {
            Parameter p = e.get_Parameter(bip);
            if (p != null && p.StorageType == StorageType.Double)
                return p.AsDouble();
            return null;
        }

        /// <summary>
        /// Resolve a double-valued parameter by trying a list of BuiltInParameters first,
        /// then a list of localized display names (covers both Chinese and English
        /// Revit UI locales), then a full parameter scan by name.
        /// </summary>
        public static double? GetParamValue(Element e, BuiltInParameter[] bips, string[] names)
        {
            if (e == null) return null;

            foreach (BuiltInParameter bip in bips)
            {
                var val = GetParamDouble(e, bip);
                if (val.HasValue) return val;
            }

            foreach (var name in names)
            {
                Parameter p = e.LookupParameter(name);
                if (p != null && p.StorageType == StorageType.Double)
                    return p.AsDouble();
            }

            foreach (Parameter param in e.Parameters)
            {
                if (param.StorageType != StorageType.Double) continue;
                string paramName = param.Definition.Name;
                foreach (var name in names)
                {
                    if (paramName == name) return param.AsDouble();
                }
            }

            return null;
        }

        /// <summary>
        /// Classify a window family/type name into an opening mechanism and its
        /// smoke-exhaust effective-area ratio. Ported from REVIT_MCP_study's
        /// GetWindowOperationType (CommandExecutor.SmokeExhaust.cs).
        /// </summary>
        public static (string operationType, double openingRatio, bool needsConfirm, string note) GetWindowOperationType(string familyName, string typeName)
        {
            string name = ((familyName ?? "") + " " + (typeName ?? "")).ToLower();

            bool ContainsAny(string source, string[] keywords) => keywords.Any(source.Contains);

            if (ContainsAny(name, new[] { "fixed", "固定", "picture", "景觀", "fix" }))
                return ("fixed", 0, false, "Fixed window: smoke-exhaust effective area is 0");

            if (ContainsAny(name, new[] { "casement", "平開", "側開", "pivot", "樞軸", "中懸", "tilt", "內倒內開", "tiltturn" }))
                return ("casement", 1.0, false, null);

            if (ContainsAny(name, new[] { "sliding", "橫拉", "推拉", "hung", "上下拉", "單拉", "double hung", "single hung", "doublehung", "singlehung" }))
                return ("sliding", 0.5, false, "Sliding window: effective area reduced 50%");

            if (ContainsAny(name, new[] { "awning", "上懸", "外推", "hopper", "下懸", "projected" }))
                return ("projected", 0.5, false, "Projected/awning window: effective area reduced 50% (conservative)");

            if (ContainsAny(name, new[] { "louver", "百葉" }))
                return ("louver", 0.5, false, "Louver window: effective area reduced 50%");

            return ("unknown", 0, true, "Could not determine opening type from family name; manual confirmation required");
        }
    }
}
