using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Writes parameter values onto existing elements.
    ///
    /// WHY THIS EXISTS: the connector could author a model but not revise it.
    /// Parameters could be read (ai_element_filter), grouped by (color_elements)
    /// and set AT CREATION (create_*) — never updated afterwards. "Correct this
    /// wall's fire rating" had no route but raw C#.
    ///
    /// PARTIAL SUCCESS IS THE NORMAL CASE, NOT AN ERROR: read-only and
    /// type-driven parameters legitimately reject writes, and a batch that
    /// touches twenty elements will routinely have one refuse. Reporting a
    /// whole-batch failure would hide which write actually failed and why, so
    /// every rejection is returned per element+parameter with a reason.
    /// </summary>
    public class SetParameterValueEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public class ParameterUpdate
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Units { get; set; }
        }

        public class ElementUpdate
        {
            public long ElementId { get; set; }
            public List<ParameterUpdate> Parameters { get; set; }
            public bool IsTypeParameter { get; set; }
        }

        public class Failure
        {
            public long ElementId { get; set; }
            public string Parameter { get; set; }
            public string Reason { get; set; }
        }

        /// <summary>Input: elements and the parameters to write on each.</summary>
        public List<ElementUpdate> Updates { get; set; }

        public bool IsSuccess { get; private set; }
        public int UpdatedCount { get; private set; }
        public List<Failure> Failures { get; private set; } = new List<Failure>();
        public bool TaskCompleted { get; private set; }

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                UpdatedCount = 0;
                Failures = new List<Failure>();

                if (Updates == null || Updates.Count == 0)
                {
                    IsSuccess = false;
                    return;
                }

                // ONE transaction for the whole batch: a half-applied revision is
                // worse than a rejected one, and Revit's undo stack should show a
                // single reversible step rather than N of them.
                using (var transaction = new Transaction(doc, "Set Parameter Values"))
                {
                    transaction.Start();
                    foreach (var update in Updates)
                    {
                        var element = doc.GetElement(new ElementId((int)update.ElementId));
                        if (element == null)
                        {
                            AddFailure(update.ElementId, null, "element not found");
                            continue;
                        }

                        // A type parameter is shared by every instance of the type;
                        // writing it through an instance would silently change all
                        // of them, so it has to be an explicit opt-in.
                        var target = element;
                        if (update.IsTypeParameter)
                        {
                            var typeId = element.GetTypeId();
                            if (typeId == ElementId.InvalidElementId)
                            {
                                AddFailure(update.ElementId, null, "element has no type");
                                continue;
                            }
                            target = doc.GetElement(typeId);
                            if (target == null)
                            {
                                AddFailure(update.ElementId, null, "element type not found");
                                continue;
                            }
                        }

                        foreach (var p in update.Parameters ?? new List<ParameterUpdate>())
                        {
                            ApplyOne(target, update.ElementId, p);
                        }
                    }
                    transaction.Commit();
                }

                // Success means the operation ran and reported truthfully. A batch
                // where every write was refused still ran; the caller reads
                // `failures` to learn that, which is more useful than a bare false.
                IsSuccess = true;
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                Failures.Add(new Failure { Parameter = null, Reason = $"transaction failed: {ex.Message}" });
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private void AddFailure(long elementId, string parameter, string reason)
        {
            Failures.Add(new Failure { ElementId = elementId, Parameter = parameter, Reason = reason });
        }

        private void ApplyOne(Element target, long reportedId, ParameterUpdate p)
        {
            if (string.IsNullOrWhiteSpace(p?.Name))
            {
                AddFailure(reportedId, null, "parameter name is empty");
                return;
            }

            // LookupParameter matches by display name, which is what a user (and
            // therefore a model) actually knows. BuiltInParameter lookup would be
            // more precise but is not addressable from a plain name.
            var param = target.LookupParameter(p.Name);
            if (param == null)
            {
                AddFailure(reportedId, p.Name, "parameter not found on element");
                return;
            }
            if (param.IsReadOnly)
            {
                AddFailure(reportedId, p.Name, "parameter is read-only");
                return;
            }

            try
            {
                bool ok;
                switch (param.StorageType)
                {
                    case StorageType.String:
                        ok = param.Set(p.Value ?? string.Empty);
                        break;

                    case StorageType.Integer:
                        if (int.TryParse(p.Value, out int i))
                        {
                            ok = param.Set(i);
                        }
                        else if (bool.TryParse(p.Value, out bool b))
                        {
                            // Revit stores Yes/No parameters as integers.
                            ok = param.Set(b ? 1 : 0);
                        }
                        else
                        {
                            AddFailure(reportedId, p.Name, $"expected an integer, got '{p.Value}'");
                            return;
                        }
                        break;

                    case StorageType.Double:
                        if (!double.TryParse(p.Value, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out double d))
                        {
                            AddFailure(reportedId, p.Name, $"expected a number, got '{p.Value}'");
                            return;
                        }
                        // Revit stores lengths in decimal FEET internally. The MCP
                        // schema declares millimetres, so an unconverted write is
                        // wrong by a factor of ~305 — silently, because the write
                        // itself succeeds.
                        ok = param.Set(ConvertToInternal(param, d, p.Units));
                        break;

                    case StorageType.ElementId:
                        if (!int.TryParse(p.Value, out int idVal))
                        {
                            AddFailure(reportedId, p.Name, $"expected an ElementId, got '{p.Value}'");
                            return;
                        }
                        ok = param.Set(new ElementId(idVal));
                        break;

                    default:
                        AddFailure(reportedId, p.Name, $"unsupported storage type {param.StorageType}");
                        return;
                }

                if (ok)
                {
                    UpdatedCount++;
                }
                else
                {
                    AddFailure(reportedId, p.Name, "Revit rejected the value");
                }
            }
            catch (Exception ex)
            {
                AddFailure(reportedId, p.Name, ex.Message);
            }
        }

        /// <summary>
        /// Convert a caller-supplied value into Revit's internal units.
        /// Defaults to millimetres because that is what every create_* tool in
        /// this connector documents; an explicit `units` overrides it.
        /// </summary>
        private static double ConvertToInternal(Parameter param, double value, string units)
        {
            var unit = string.IsNullOrWhiteSpace(units) ? "mm" : units.Trim().ToLowerInvariant();
            switch (unit)
            {
                case "ft":
                case "feet":
                    return value;                 // already internal
                case "m":
                case "meter":
                case "metre":
                    return value * 1000.0 / 304.8;
                case "cm":
                    return value * 10.0 / 304.8;
                case "mm":
                case "millimeter":
                case "millimetre":
                    return value / 304.8;
                default:
                    // An unrecognised unit must not be guessed at: passing the raw
                    // number through is the one behaviour that cannot be silently
                    // wrong by a scale factor.
                    return value;
            }
        }
    }
}
