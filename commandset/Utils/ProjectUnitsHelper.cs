using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPCommandSet.Utils
{
    public static class ProjectUnitsHelper
    {
        private static readonly Dictionary<string, ForgeTypeId> _lengthUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "m", UnitTypeId.Meters }, { "meter", UnitTypeId.Meters }, { "meters", UnitTypeId.Meters },
            { "mm", UnitTypeId.Millimeters }, { "millimeter", UnitTypeId.Millimeters },
            { "cm", UnitTypeId.Centimeters },
            { "ft", UnitTypeId.Feet }, { "feet", UnitTypeId.Feet },
            { "ft-in", UnitTypeId.FeetFractionalInches }, { "feet-inches", UnitTypeId.FeetFractionalInches },
        };

        private static readonly Dictionary<string, ForgeTypeId> _areaUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "m2", UnitTypeId.SquareMeters }, { "sqm", UnitTypeId.SquareMeters }, { "m^2", UnitTypeId.SquareMeters },
            { "sf", UnitTypeId.SquareFeet }, { "ft2", UnitTypeId.SquareFeet }, { "sqft", UnitTypeId.SquareFeet },
        };

        private static readonly Dictionary<string, ForgeTypeId> _volumeUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "m3", UnitTypeId.CubicMeters }, { "cbm", UnitTypeId.CubicMeters }, { "m^3", UnitTypeId.CubicMeters },
            { "l", UnitTypeId.Liters }, { "liter", UnitTypeId.Liters },
            { "cf", UnitTypeId.CubicFeet }, { "ft3", UnitTypeId.CubicFeet },
        };

        private static readonly Dictionary<string, ForgeTypeId> _airFlowUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "m3/h", UnitTypeId.CubicMetersPerHour }, { "m3h", UnitTypeId.CubicMetersPerHour }, { "cmh", UnitTypeId.CubicMetersPerHour },
            { "l/s", UnitTypeId.LitersPerSecond }, { "lps", UnitTypeId.LitersPerSecond },
            { "cfm", UnitTypeId.CubicFeetPerMinute },
        };

        private static readonly Dictionary<string, ForgeTypeId> _pipeSizeUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "mm", UnitTypeId.Millimeters }, { "cm", UnitTypeId.Centimeters }, { "m", UnitTypeId.Meters },
            { "in", UnitTypeId.Inches }, { "inch", UnitTypeId.Inches },
        };

        private static readonly Dictionary<string, ForgeTypeId> _flowUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "l/min", UnitTypeId.LitersPerMinute }, { "lpm", UnitTypeId.LitersPerMinute },
            { "l/s", UnitTypeId.LitersPerSecond }, { "lps", UnitTypeId.LitersPerSecond },
            { "m3/h", UnitTypeId.CubicMetersPerHour },
            { "gpm", UnitTypeId.UsGallonsPerMinute },
        };

        private static readonly Dictionary<string, ForgeTypeId> _pipeVelocityUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "m/s", UnitTypeId.MetersPerSecond }, { "mps", UnitTypeId.MetersPerSecond },
            { "fps", UnitTypeId.FeetPerSecond },
        };

        private static readonly Dictionary<string, ForgeTypeId> _pipePressureUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "mh2o", UnitTypeId.MetersOfWaterColumn }, { "m-h2o", UnitTypeId.MetersOfWaterColumn }, { "mwc", UnitTypeId.MetersOfWaterColumn },
            { "mmh2o", UnitTypeId.MillimetersOfWaterColumn },
            { "kpa", UnitTypeId.Kilopascals }, { "pa", UnitTypeId.Pascals },
            { "bar", UnitTypeId.Bars },
            { "kgf/m2", UnitTypeId.KilogramsForcePerSquareMeter },
        };

        private static readonly Dictionary<string, ForgeTypeId> _pipeFrictionUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "mmh2o/m", UnitTypeId.MillimetersOfWaterColumnPerMeter }, { "mmwc/m", UnitTypeId.MillimetersOfWaterColumnPerMeter },
            { "mh2o/m", UnitTypeId.MetersOfWaterColumnPerMeter },
            { "pa/m", UnitTypeId.PascalsPerMeter },
        };

        private static readonly Dictionary<string, ForgeTypeId> _pipeSlopeUnitMap = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase)
        {
            { "1:ratio", UnitTypeId.OneToRatio }, { "one-to-ratio", UnitTypeId.OneToRatio }, { "1:n", UnitTypeId.OneToRatio },
            { "ratio:1", UnitTypeId.RatioTo1 }, { "n:1", UnitTypeId.RatioTo1 },
            { "%", UnitTypeId.Percentage }, { "percent", UnitTypeId.Percentage },
            { "deg", UnitTypeId.SlopeDegrees }, { "degrees", UnitTypeId.SlopeDegrees },
        };

        public static object Run(Document doc, JObject parameters)
        {
            string mode = parameters?["mode"]?.Value<string>()?.Trim().ToLowerInvariant();
            string system = parameters?["system"]?.Value<string>()?.Trim().ToLowerInvariant();

            bool isTaiwanPlumbing =
                mode == "taiwan-plumbing" || mode == "taiwan_plumbing" ||
                mode == "taiwanplumbing" || mode == "tw-plumbing";
            bool isTaiwan = mode == "taiwan" || isTaiwanPlumbing;

            UnitSystem baseSystem = UnitSystem.Metric;
            if (mode == "imperial" || system == "imperial")
                baseSystem = UnitSystem.Imperial;

            Units units = new Units(baseSystem);
            var applied = new List<object>();

            if (isTaiwan)
            {
                ApplyFormat(units, SpecTypeId.AirFlow, "airFlow",
                    UnitTypeId.CubicMetersPerHour, null, 0.1, applied, "mode=" + mode);

                ApplyFormat(units, SpecTypeId.Length, "length",
                    UnitTypeId.Millimeters, SymbolTypeId.Mm, 1.0, applied, "mode=" + mode);
            }

            if (isTaiwanPlumbing)
            {
                ApplyFormat(units, SpecTypeId.PipeSize, "pipeSize",
                    UnitTypeId.Millimeters, SymbolTypeId.Mm, 1.0, applied, "mode=" + mode);

                ApplyFormat(units, SpecTypeId.Flow, "flow",
                    UnitTypeId.LitersPerMinute, SymbolTypeId.LPerMin, 0.1, applied, "mode=" + mode);

                ApplyFormat(units, SpecTypeId.PipingVelocity, "velocity",
                    UnitTypeId.MetersPerSecond, SymbolTypeId.MPerS, 0.01, applied, "mode=" + mode);

                ApplyFormat(units, SpecTypeId.PipingPressure, "pressure",
                    UnitTypeId.MetersOfWaterColumn, SymbolTypeId.MH2O, 0.01, applied, "mode=" + mode);

                ApplyFormat(units, SpecTypeId.PipingFriction, "friction",
                    UnitTypeId.MillimetersOfWaterColumnPerMeter, SymbolTypeId.MmH2OPerM, 0.1, applied, "mode=" + mode);

                ApplyFormat(units, SpecTypeId.PipingSlope, "slope",
                    UnitTypeId.OneToRatio, SymbolTypeId.OneColon, 0.01, applied, "mode=" + mode);
            }

            ApplyUnitOverride(units, parameters, "length", SpecTypeId.Length, _lengthUnitMap, applied);
            ApplyUnitOverride(units, parameters, "area", SpecTypeId.Area, _areaUnitMap, applied);
            ApplyUnitOverride(units, parameters, "volume", SpecTypeId.Volume, _volumeUnitMap, applied);
            ApplyUnitOverride(units, parameters, "airFlow", SpecTypeId.AirFlow, _airFlowUnitMap, applied);
            ApplyUnitOverride(units, parameters, "pipeSize", SpecTypeId.PipeSize, _pipeSizeUnitMap, applied);
            ApplyUnitOverride(units, parameters, "flow", SpecTypeId.Flow, _flowUnitMap, applied);
            ApplyUnitOverride(units, parameters, "velocity", SpecTypeId.PipingVelocity, _pipeVelocityUnitMap, applied);
            ApplyUnitOverride(units, parameters, "pressure", SpecTypeId.PipingPressure, _pipePressureUnitMap, applied);
            ApplyUnitOverride(units, parameters, "friction", SpecTypeId.PipingFriction, _pipeFrictionUnitMap, applied);
            ApplyUnitOverride(units, parameters, "slope", SpecTypeId.PipingSlope, _pipeSlopeUnitMap, applied);

            using (Transaction t = new Transaction(doc, "Set Project Units"))
            {
                t.Start();
                doc.SetUnits(units);
                t.Commit();
            }

            Units after = doc.GetUnits();

            return new
            {
                Success = true,
                Mode = mode ?? (system ?? "metric"),
                BaseSystem = baseSystem.ToString(),
                Applied = applied,
                Result = new
                {
                    Length = ReportFormat(after, SpecTypeId.Length),
                    Area = ReportFormat(after, SpecTypeId.Area),
                    Volume = ReportFormat(after, SpecTypeId.Volume),
                    AirFlow = ReportFormat(after, SpecTypeId.AirFlow),
                    PipeSize = ReportFormat(after, SpecTypeId.PipeSize),
                    Flow = ReportFormat(after, SpecTypeId.Flow),
                    Velocity = ReportFormat(after, SpecTypeId.PipingVelocity),
                    Pressure = ReportFormat(after, SpecTypeId.PipingPressure),
                    Friction = ReportFormat(after, SpecTypeId.PipingFriction),
                    Slope = ReportFormat(after, SpecTypeId.PipingSlope),
                },
                Note = isTaiwanPlumbing
                    ? "Pressure is represented via mH2O (1 kgf/cm2 = 10.0 mH2O)."
                    : null,
                Message = "Project units updated successfully."
            };
        }

        private static void ApplyFormat(
            Units units, ForgeTypeId spec, string label,
            ForgeTypeId unit, ForgeTypeId symbol, double accuracy,
            List<object> applied, string from)
        {
            string symbolStatus = symbol == null ? "not-requested" : "pending";
            FormatOptions fo;

            try
            {
                fo = new FormatOptions(unit);
            }
            catch (Exception ex)
            {
                applied.Add(new { spec = label, from, error = "Failed to create FormatOptions: " + ex.Message });
                return;
            }

            try { fo.Accuracy = accuracy; }
            catch (Exception ex) { applied.Add(new { spec = label, from, warning = "Failed to set accuracy: " + ex.Message }); }

            if (symbol != null)
            {
                try { fo.SetSymbolTypeId(symbol); symbolStatus = "set"; }
                catch (Exception ex) { symbolStatus = "failed: " + ex.Message; }
            }

            try
            {
                units.SetFormatOptions(spec, fo);
                applied.Add(new { spec = label, unit = SafeTypeId(unit), symbol = symbolStatus, accuracy, from });
            }
            catch (Exception ex)
            {
                applied.Add(new { spec = label, unit = SafeTypeId(unit), symbol = symbolStatus, accuracy, from, error = ex.Message });
            }
        }

        private static void ApplyUnitOverride(
            Units units, JObject parameters, string paramKey,
            ForgeTypeId spec, Dictionary<string, ForgeTypeId> map, List<object> applied)
        {
            string v = parameters?[paramKey]?.Value<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(v)) return;

            if (!map.TryGetValue(v, out ForgeTypeId unitId))
                throw new ArgumentException("Unsupported " + paramKey + " unit '" + v + "'. Available: " + string.Join(", ", map.Keys));

            double accuracy = 0.01;
            ForgeTypeId symbol = null;
            try
            {
                FormatOptions prev = units.GetFormatOptions(spec);
                accuracy = prev.Accuracy;
                try
                {
                    ForgeTypeId s = prev.GetSymbolTypeId();
                    if (s != null && !string.IsNullOrEmpty(s.TypeId)) symbol = s;
                }
                catch { }
            }
            catch { }

            ApplyFormat(units, spec, paramKey, unitId, symbol, accuracy, applied, "override");
        }

        private static string SafeTypeId(ForgeTypeId id)
        {
            try { return id == null ? null : id.TypeId; } catch { return null; }
        }

        private static object ReportFormat(Units units, ForgeTypeId spec)
        {
            try
            {
                FormatOptions fo = units.GetFormatOptions(spec);
                string sym = null;
                try
                {
                    ForgeTypeId s = fo.GetSymbolTypeId();
                    if (s != null && !string.IsNullOrEmpty(s.TypeId)) sym = s.TypeId;
                }
                catch { }

                return new { unit = SafeTypeId(fo.GetUnitTypeId()), symbol = sym, accuracy = fo.Accuracy };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }
    }
}
