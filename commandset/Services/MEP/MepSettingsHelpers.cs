using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services.MEP
{
    /// <summary>
    /// Shared read-only helpers for get_mep_segments_and_sizes and get_mep_settings.
    /// Ported from REVIT_MCP_study MCP/Core/Commands/CommandExecutor.MepSettings.cs.
    /// Lengths are converted from internal units (feet) to mm, angles to degrees; physical
    /// quantities also carry a project-display-unit-formatted string.
    /// </summary>
    public static class MepSettingsHelpers
    {
        /// <summary>Internal units (feet) to mm.</summary>
        public static double ToMm(double internalValue, int digits = 4)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters), digits);
        }

        /// <summary>Name of the element referenced by id, or null if invalid/missing.</summary>
        public static string GetElementNameOrNull(Document doc, ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId) return null;
            Element element = doc.GetElement(id);
            return element?.Name;
        }

        /// <summary>Walk DuctSizes via its native iterator (no generic IEnumerable on this type).</summary>
        public static IEnumerable<MEPSize> EnumerateDuctSizes(DuctSizes ductSizes)
        {
            DuctSizeIterator iterator = ductSizes.GetDuctSizeIterator();
            iterator.Reset();
            while (iterator.MoveNext())
            {
                MEPSize current = iterator.Current;
                if (current != null) yield return current;
            }
        }

        /// <summary>Pipe size (nominal/inner/outer are all real physical values).</summary>
        public static object DescribeSize(MEPSize size)
        {
            return new
            {
                nominal_mm = ToMm(size.NominalDiameter),
                inner_mm = ToMm(size.InnerDiameter),
                outer_mm = ToMm(size.OuterDiameter),
                usedInSizeLists = size.UsedInSizeLists,
                usedInSizing = size.UsedInSizing,
            };
        }

        /// <summary>
        /// Duct size — deliberately omits inner/outer: Revit's duct size table has only a
        /// nominal dimension, and MEPSize.Inner/OuterDiameter are fixed placeholder values for
        /// ducts (observed as 12 ft for all three shapes), so surfacing them would mislead.
        /// </summary>
        public static object DescribeDuctSize(MEPSize size)
        {
            return new
            {
                nominal_mm = ToMm(size.NominalDiameter),
                usedInSizeLists = size.UsedInSizeLists,
                usedInSizing = size.UsedInSizing,
            };
        }

        public static string BuildMepInventoryMessage(
            int shown, int total, int totalSizes, int reportedSizes,
            bool usedOnly, bool summaryOnly, bool hasNameFilter)
        {
            string scope = hasNameFilter
                ? $"{shown} segment(s) matching the name filter (of {total} total)"
                : $"{shown} segment(s)";

            string sizePart = summaryOnly
                ? $", counts only ({totalSizes} size(s) not listed individually)"
                : usedOnly
                    ? $"; only sizes with Used in Size Lists / Used in Sizing checked ({reportedSizes}/{totalSizes})"
                    : $", {totalSizes} pipe size(s)";

            return scope + sizePart + ". All sizes are in mm.";
        }

        /// <summary>Wrap a single settings-page read so a failure there doesn't fail the whole tool.</summary>
        public static object MepSafeRead(Func<object> read, out string note)
        {
            try
            {
                note = null;
                return read();
            }
            catch (Exception ex)
            {
                note = $"Read failed: {ex.Message}";
                return null;
            }
        }

        /// <summary>Format a value using the project's display units; null (not a throw) if the spec doesn't apply.</summary>
        public static string MepFormatValue(Units units, ForgeTypeId spec, double value, bool forEditing = false)
        {
            try { return UnitFormatUtils.Format(units, spec, value, forEditing); }
            catch { return null; }
        }

        /// <summary>
        /// Angle list + enabled flags. GetSpecificFittingAngles() already returns DEGREES, not
        /// Revit's internal radians — converting again would multiply by ~57.3.
        /// GetSpecificFittingAngleStatus must be fed the raw (un-converted) value too.
        /// </summary>
        public static List<object> MepDescribeAngles(IList<double> angles, Func<double, bool> statusOf)
        {
            var result = new List<object>();
            if (angles == null) return result;

            foreach (double angle in angles.OrderBy(a => a))
            {
                bool enabled;
                try { enabled = statusOf(angle); }
                catch { continue; }

                result.Add(new { deg = Math.Round(angle, 4), enabled });
            }

            return result;
        }

        /// <summary>
        /// Scan a settings element for parameters whose name mentions "angle" — the increment
        /// value used when FittingAngleUsage=UseAnAngleIncrement has no BuiltInParameter
        /// (confirmed absent via reflection on R22/R26), so this generic scan is the only way to surface it.
        /// </summary>
        public static List<object> MepScanAngleParameters(Element settingsElement)
        {
            var result = new List<object>();
            if (settingsElement == null) return result;

            foreach (Parameter p in settingsElement.Parameters)
            {
                string name = p?.Definition?.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.IndexOf("angle", StringComparison.OrdinalIgnoreCase) < 0) continue;

                result.Add(new { name, value = MepReadParameterValue(p) });
            }

            return result;
        }

        /// <summary>Prefer the display string; fall back by StorageType if that's empty.</summary>
        public static object MepReadParameterValue(Parameter p)
        {
            try
            {
                string display = p.AsValueString();
                if (!string.IsNullOrWhiteSpace(display)) return display;

                switch (p.StorageType)
                {
                    case StorageType.Double: return p.AsDouble();
                    case StorageType.Integer: return p.AsInteger();
                    case StorageType.String: return p.AsString();
                    case StorageType.ElementId: return p.AsElementId()?.GetIntValue();
                    default: return null;
                }
            }
            catch { return null; }
        }

        public static object MepDescribeElevationText(
            string centerline, string setUp, string setDown,
            string setUpFromBottom, string setDownFromBottom, string flatOnTop, string flatOnBottom)
        {
            return new { centerline, setUp, setDown, setUpFromBottom, setDownFromBottom, flatOnTop, flatOnBottom };
        }

        /// <summary>Duct Settings: Angles / Calculation / size naming & annotation / elevation text.</summary>
        public static object MepReadDuctSettings(Document doc, Units units)
        {
            DuctSettings ds = DuctSettings.GetDuctSettings(doc);
            if (ds == null) return null;

            // NOTE (low confidence / needs verification against the actual Revit 2025/2026 SDK):
            // REVIT_MCP_study reports DuctSettings.AirViscosity was renamed to AirDynamicViscosity
            // starting Revit 2025 and removed outright in 2026, gated there by a REVIT2025_OR_GREATER
            // constant that project defines. This repo's csproj does not define REVIT2025_OR_GREATER
            // for ANY configuration (R25/R26 configs only go up to REVIT2024_OR_GREATER), so an #if on
            // that symbol here would always take the old-name branch, including for R26. Using the
            // pre-2025 name unconditionally until this is checked against the actual R25/R26 Nice3point
            // API package; if it no longer compiles there, rename to AirDynamicViscosity for those two
            // configs specifically (may need the missing REVIT2025_OR_GREATER constant added to the csproj).
            #if REVIT2025_OR_GREATER
            double airViscosity = ds.AirDynamicViscosity;
#else
            double airViscosity = ds.AirViscosity;
#endif
#if REVIT2024_OR_GREATER
            bool? networkBasedCalculations = ds.NetworkBasedCalculations;
#else
            bool? networkBasedCalculations = null;
#endif

            return new
            {
                fittingAngleUsage = ds.FittingAngleUsage.ToString(),
                specificAnglesInEffect = ds.FittingAngleUsage == FittingAngleUsage.UseSpecificAngles,
                specificAngles = MepDescribeAngles(ds.GetSpecificFittingAngles(), ds.GetSpecificFittingAngleStatus),
                otherAngleParameters = MepScanAngleParameters(ds),
                calculation = new
                {
                    airDensity = new { raw = ds.AirDensity, display = MepFormatValue(units, SpecTypeId.MassDensity, ds.AirDensity) },
                    airViscosity = new { raw = airViscosity, display = MepFormatValue(units, SpecTypeId.HvacViscosity, airViscosity) },
                    networkBasedCalculations,
                },
                annotation = new
                {
                    useAnnotationScaleForSingleLineFittings = ds.UseAnnotationScaleForSingleLineFittings,
                    riseDropAnnotationSize_mm = ToMm(ds.RiseDropAnnotationSize),
                    fittingAnnotationSize_mm = ToMm(ds.FittingAnnotationSize),
                },
                sizeNaming = new
                {
                    roundPrefix = ds.RoundDuctSizePrefix,
                    roundSuffix = ds.RoundDuctSizeSuffix,
                    rectangularSeparator = ds.RectangularDuctSizeSeparator,
                    rectangularSuffix = ds.RectangularDuctSizeSuffix,
                    ovalSeparator = ds.OvalDuctSizeSeparator,
                    ovalSuffix = ds.OvalDuctSizeSuffix,
                    connectorSeparator = ds.ConnectorSeparator,
                },
                elevationText = MepDescribeElevationText(
                    ds.Centerline, ds.SetUp, ds.SetDown, ds.SetUpFromBottom, ds.SetDownFromBottom, ds.FlatOnTop, ds.FlatOnBottom),
            };
        }

        /// <summary>Pipe Settings: Angles / Slopes / Fluids / Calculation / size naming / elevation text.</summary>
        public static object MepReadPipeSettings(Document doc, Units units, bool includeFluids, bool includeFluidTemperatures)
        {
            PipeSettings ps = PipeSettings.GetPipeSettings(doc);
            if (ps == null) return null;

            // GetPipeSlopes() returns PERCENT, not Revit's internal ratio (observed 0 / 1.0417 /
            // 2.0833 / 4.1667 == 1/8", 1/4", 1/2" per 12"). `display` reflects the project's slope
            // display precision and can collapse distinct slopes to the same string, so `percent`
            // and `ratio_1_in` are the values to trust.
            var slopes = new List<object>();
            foreach (double slope in ps.GetPipeSlopes())
            {
                slopes.Add(new
                {
                    percent = Math.Round(slope, 6),
                    ratio_1_in = slope > 0 ? (double?)Math.Round(100.0 / slope, 2) : null,
                    display = MepFormatValue(units, SpecTypeId.Slope, slope / 100.0),
                });
            }

            return new
            {
                fittingAngleUsage = ps.FittingAngleUsage.ToString(),
                specificAnglesInEffect = ps.FittingAngleUsage == FittingAngleUsage.UseSpecificAngles,
                specificAngles = MepDescribeAngles(ps.GetSpecificFittingAngles(), ps.GetSpecificFittingAngleStatus),
                otherAngleParameters = MepScanAngleParameters(ps),
                slopes,
                slopesNote = "percent and ratio_1_in are exact; display is rounded to the project's slope display precision and different slopes can render as the same string.",
                fluids = includeFluids ? MepReadFluidTypes(doc, units, includeFluidTemperatures) : null,
                calculation = new
                {
                    analysisForClosedLoopHydronicPipingNetworks = ps.AnalysisForClosedLoopHydronicPipingNetworks,
                    // ConnectorTolerance is an ANGLE, not a length (raw=0.0872665 is exactly 5 degrees
                    // in radians) — converting it as a length yields a meaningless 26.6 mm.
                    connectorTolerance = new
                    {
                        raw = ps.ConnectorTolerance,
                        deg = Math.Round(UnitUtils.ConvertFromInternalUnits(ps.ConnectorTolerance, UnitTypeId.Degrees), 4),
                        display = MepFormatValue(units, SpecTypeId.Angle, ps.ConnectorTolerance),
                    },
                },
                annotation = new
                {
                    useAnnotationScaleForSingleLineFittings = ps.UseAnnotationScaleForSingleLineFittings,
                    fittingAnnotationSize_mm = ToMm(ps.FittingAnnotationSize),
                },
                sizeNaming = new
                {
                    sizePrefix = ps.SizePrefix,
                    sizeSuffix = ps.SizeSuffix,
                    connectorSeparator = ps.ConnectorSeparator,
                },
                elevationText = MepDescribeElevationText(
                    ps.Centerline, ps.SetUp, ps.SetDown, ps.SetUpFromBottom, ps.SetDownFromBottom, ps.FlatOnTop, ps.FlatOnBottom),
            };
        }

        /// <summary>Fluids page: each fluid type and (optionally) its temperature/viscosity/density table.</summary>
        public static List<object> MepReadFluidTypes(Document doc, Units units, bool includeTemperatures)
        {
            var fluids = new List<object>();

            var fluidTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FluidType))
                .Cast<FluidType>()
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase);

            foreach (FluidType fluid in fluidTypes)
            {
                var temperatures = new List<object>();
                int count = 0;

                FluidTemperatureSetIterator iterator = fluid.GetFluidTemperatureSetIterator();
                iterator.Reset();
                while (iterator.MoveNext())
                {
                    FluidTemperature ft = iterator.Current;
                    if (ft == null) continue;
                    count++;
                    if (!includeTemperatures) continue;

                    temperatures.Add(new
                    {
                        // Revit stores temperature internally in Kelvin.
                        temperature_K = Math.Round(ft.Temperature, 4),
                        temperature_C = Math.Round(ft.Temperature - 273.15, 4),
                        viscosity = new { raw = ft.Viscosity, display = MepFormatValue(units, SpecTypeId.HvacViscosity, ft.Viscosity) },
                        density = new { raw = ft.Density, display = MepFormatValue(units, SpecTypeId.MassDensity, ft.Density) },
                    });
                }

                fluids.Add(new
                {
                    id = fluid.Id.GetIntValue(),
                    name = fluid.Name,
                    temperatureCount = count,
                    inUse = FluidType.IsFluidInUse(doc, fluid.Id),
                    temperatures = includeTemperatures ? temperatures : null,
                });
            }

            return fluids;
        }

        public static object MepReadHiddenLineSettings(Document doc, Units units)
        {
            MEPHiddenLineSettings hl = MEPHiddenLineSettings.GetMEPHiddenLineSettings(doc);
            if (hl == null) return null;

            return new
            {
                drawHiddenLine = hl.DrawHiddenLine,
                lineStyle = GetElementNameOrNull(doc, hl.LineStyle),
                singleLineGap_mm = ToMm(hl.SingleLineGap),
                outsideGap_mm = ToMm(hl.OutsideGap),
                insideGap_mm = ToMm(hl.InsideGap),
            };
        }
    }
}
