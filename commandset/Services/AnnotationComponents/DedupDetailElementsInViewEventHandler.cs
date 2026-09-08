using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.AnnotationComponents
{
    public class DedupDetailElementsInViewEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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

                long viewIdValue = _parameters?["viewId"]?.Value<long>() ?? 0;
                View targetView;
                if (viewIdValue == 0)
                {
                    targetView = doc.ActiveView;
                    if (targetView == null) throw new InvalidOperationException("No active view, please specify viewId");
                }
                else
                {
                    targetView = doc.GetElement(viewIdValue.ToElementId()) as View;
                    if (targetView == null) throw new ArgumentException($"View ID not found: {viewIdValue}");
                }
                if (targetView.IsTemplate) throw new InvalidOperationException($"View '{targetView.Name}' is a ViewTemplate, cannot process");

                var categoriesArr = _parameters?["categories"]?.ToObject<List<string>>() ?? new List<string> { "All" };
                var categories = new HashSet<string>(categoriesArr, StringComparer.OrdinalIgnoreCase);
                bool processAll = categories.Contains("All");

                double toleranceMm = _parameters?["tolerance"]?.Value<double>() ?? 1.0;
                if (toleranceMm <= 0) toleranceMm = 1.0;

                bool dryRun = _parameters?["dryRun"] == null ? true : _parameters["dryRun"].Value<bool>();

                var collected = new List<Element>();
                int detailComponentCount = 0, detailCurveCount = 0, filledRegionCount = 0, textNoteCount = 0, dimensionCount = 0;

                if (processAll || categories.Contains("DetailComponent"))
                {
                    var items = new FilteredElementCollector(doc, targetView.Id)
                        .OfCategory(BuiltInCategory.OST_DetailComponents)
                        .WhereElementIsNotElementType()
                        .ToList();
                    detailComponentCount = items.Count;
                    collected.AddRange(items);
                }
                if (processAll || categories.Contains("DetailCurve"))
                {
                    var items = new FilteredElementCollector(doc, targetView.Id)
                        .OfClass(typeof(CurveElement))
                        .Where(e => e is DetailCurve)
                        .ToList();
                    detailCurveCount = items.Count;
                    collected.AddRange(items);
                }
                if (processAll || categories.Contains("FilledRegion"))
                {
                    var items = new FilteredElementCollector(doc, targetView.Id)
                        .OfClass(typeof(FilledRegion))
                        .ToList();
                    filledRegionCount = items.Count;
                    collected.AddRange(items);
                }
                if (processAll || categories.Contains("TextNote"))
                {
                    var items = new FilteredElementCollector(doc, targetView.Id)
                        .OfClass(typeof(TextNote))
                        .ToList();
                    textNoteCount = items.Count;
                    collected.AddRange(items);
                }
                if (processAll || categories.Contains("Dimension"))
                {
                    var items = new FilteredElementCollector(doc, targetView.Id)
                        .OfClass(typeof(Dimension))
                        .WhereElementIsNotElementType()
                        .ToList();
                    dimensionCount = items.Count;
                    collected.AddRange(items);
                }

                var memberToDetailGroup = new Dictionary<long, GroupRef>();
                var detailGroups = new FilteredElementCollector(doc, targetView.Id)
                    .OfClass(typeof(Group))
                    .Cast<Group>()
                    .Where(g => g.Category != null
                                && g.Category.Id.GetIntValue() == (int)BuiltInCategory.OST_IOSDetailGroups)
                    .ToList();

                foreach (var g in detailGroups)
                {
                    string gName = g.Name;
                    long gId = g.Id.GetIntValue();
                    foreach (var mid in g.GetMemberIds())
                    {
                        memberToDetailGroup[mid.GetIntValue()] = new GroupRef { GroupId = gId, GroupName = gName };
                    }
                }

                var keyed = new List<KeyedElem>();
                int unkeyedCount = 0;
                foreach (var el in collected)
                {
                    string typeKey = ComputeTypeKey(el);
                    string posKey = ComputePositionKey(el, toleranceMm, targetView);
                    if (typeKey == null || posKey == null) { unkeyedCount++; continue; }
                    keyed.Add(new KeyedElem { Elem = el, TypeKey = typeKey, PosKey = posKey });
                }

                var dupGroups = keyed
                    .GroupBy(k => k.TypeKey + "||" + k.PosKey)
                    .Where(g => g.Count() > 1)
                    .ToList();

                var duplicateGroups = new List<object>();
                var toDeleteIds = new List<long>();
                int ambiguousCount = 0;
                int allInGroupsCount = 0;
                int toDedupGroupCount = 0;

                foreach (var grp in dupGroups)
                {
                    var members = grp.ToList();
                    var inGrp = members.Where(m => memberToDetailGroup.ContainsKey(m.Elem.Id.GetIntValue())).ToList();
                    var outGrp = members.Where(m => !memberToDetailGroup.ContainsKey(m.Elem.Id.GetIntValue())).ToList();

                    string status;
                    if (inGrp.Count == 0)
                    {
                        status = "ambiguous_no_group";
                        ambiguousCount++;
                    }
                    else if (outGrp.Count == 0)
                    {
                        status = "all_in_groups";
                        allInGroupsCount++;
                    }
                    else
                    {
                        status = "to_dedup";
                        toDedupGroupCount++;
                        foreach (var m in outGrp)
                        {
                            toDeleteIds.Add(m.Elem.Id.GetIntValue());
                        }
                    }

                    duplicateGroups.Add(new
                    {
                        TypeKey = members[0].TypeKey,
                        PositionKey = members[0].PosKey,
                        Status = status,
                        MemberCount = members.Count,
                        InGroupCount = inGrp.Count,
                        OutGroupCount = outGrp.Count,
                        Members = members.Select(m =>
                        {
                            bool isInGroup = memberToDetailGroup.TryGetValue(m.Elem.Id.GetIntValue(), out var gi);
                            return new
                            {
                                ElementId = m.Elem.Id.GetIntValue(),
                                Category = m.Elem.Category?.Name,
                                TypeName = doc.GetElement(m.Elem.GetTypeId())?.Name,
                                InDetailGroup = isInGroup,
                                GroupId = isInGroup ? (object)gi.GroupId : null,
                                GroupName = isInGroup ? gi.GroupName : null,
                                WillBeDeleted = !isInGroup && status == "to_dedup"
                            };
                        }).ToList()
                    });
                }

                int deletedCount = 0;
                var deleteErrors = new List<object>();
                if (!dryRun && toDeleteIds.Count > 0)
                {
                    using (Transaction trans = new Transaction(doc, "Dedup detail elements"))
                    {
                        trans.Start();
                        foreach (var id in toDeleteIds)
                        {
                            try
                            {
                                var elId = id.ToElementId();
                                var el = doc.GetElement(elId);
                                if (el != null)
                                {
                                    doc.Delete(elId);
                                    deletedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                deleteErrors.Add(new { ElementId = id, Error = ex.Message });
                            }
                        }
                        trans.Commit();
                    }
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Dedup completed: {duplicateGroups.Count} duplicate groups found, {toDeleteIds.Count} items to delete",
                    Response = new
                    {
                        ViewId = targetView.Id.GetIntValue(),
                        ViewName = targetView.Name,
                        ViewType = targetView.ViewType.ToString(),
                        DryRun = dryRun,
                        ToleranceMm = toleranceMm,
                        Scanned = new
                        {
                            DetailComponent = detailComponentCount,
                            DetailCurve = detailCurveCount,
                            FilledRegion = filledRegionCount,
                            TextNote = textNoteCount,
                            Dimension = dimensionCount,
                            Total = collected.Count,
                            UnkeyedSkipped = unkeyedCount
                        },
                        DetailGroupsInView = detailGroups.Count,
                        DuplicateGroupCount = duplicateGroups.Count,
                        ToDedupGroupCount = toDedupGroupCount,
                        AmbiguousNoGroupCount = ambiguousCount,
                        AllInGroupsCount = allInGroupsCount,
                        ToDeleteCount = toDeleteIds.Count,
                        ToDeleteIds = toDeleteIds,
                        DeletedCount = deletedCount,
                        DeleteErrors = deleteErrors,
                        DuplicateGroups = duplicateGroups,
                        Notes = new[]
                        {
                            "Status='to_dedup' = will delete out-of-group copies, keep in-group copies",
                            "Status='all_in_groups' = all duplicates inside detail groups, NOT touched",
                            "Status='ambiguous_no_group' = no copy is inside any detail group, NOT touched",
                            "Tolerance is in mm; coordinates are quantized to this granularity for matching",
                            "Only Detail Groups (OST_IOSDetailGroups) are recognized; Model Groups are ignored"
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error deduping detail elements: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private string ComputeTypeKey(Element el)
        {
            if (el is DetailCurve dc)
            {
                var lsId = dc.LineStyle?.Id;
                return $"DetailCurve:LS{(lsId != null ? lsId.GetIntValue().ToString() : "null")}";
            }
            var typeId = el.GetTypeId();
            string catId = el.Category?.Id.GetIntValue().ToString() ?? "null";
            if (typeId != ElementId.InvalidElementId)
            {
                return $"Cat{catId}:T{typeId.GetIntValue()}";
            }
            return $"Cat{catId}:NoType";
        }

        private string ComputePositionKey(Element el, double toleranceMm, View view)
        {
            string Q(double mm) => Math.Round(mm / toleranceMm).ToString("R", CultureInfo.InvariantCulture);
            string Pt(XYZ p) => p == null ? "null" : $"{Q(p.X * 304.8)},{Q(p.Y * 304.8)},{Q(p.Z * 304.8)}";

            if (el is FamilyInstance fi)
            {
                var lp = fi.Location as LocationPoint;
                if (lp != null) return $"pt:{Pt(lp.Point)}";
                var bbF = TryBBox(el, view);
                if (bbF != null) return $"bbox:{Pt(bbF.Min)}|{Pt(bbF.Max)}";
                return null;
            }

            if (el is DetailCurve dc)
            {
                Curve crv = null;
                try { crv = dc.GeometryCurve; } catch { crv = null; }
                if (crv == null) return null;
                XYZ p0, p1;
                try { p0 = crv.GetEndPoint(0); p1 = crv.GetEndPoint(1); }
                catch { return null; }
                var s0 = Pt(p0);
                var s1 = Pt(p1);
                var sorted = string.Compare(s0, s1, StringComparison.Ordinal) <= 0 ? $"{s0}|{s1}" : $"{s1}|{s0}";
                return $"crv:{sorted}";
            }

            if (el is FilledRegion)
            {
                var bb = TryBBox(el, view);
                if (bb == null) return null;
                return $"fr:{Pt(bb.Min)}|{Pt(bb.Max)}";
            }

            if (el is TextNote tn)
            {
                XYZ coord = null;
                try { coord = tn.Coord; } catch { coord = null; }
                string text = tn.Text ?? "";
                int textHash = text.GetHashCode();
                return $"text:{Pt(coord)}|L{text.Length}|H{textHash}";
            }

            if (el is Dimension dim)
            {
                XYZ center = null;
                var bb = TryBBox(el, view);
                if (bb != null) center = (bb.Min + bb.Max) * 0.5;
                else
                {
                    try { center = dim.Origin; } catch { center = null; }
                }
                string val = "";
                try { val = dim.ValueString ?? ""; } catch { }
                return $"dim:{Pt(center)}|V{val}";
            }

            var def = TryBBox(el, view);
            if (def == null) return null;
            return $"bbox:{Pt(def.Min)}|{Pt(def.Max)}";
        }

        private static BoundingBoxXYZ TryBBox(Element el, View view)
        {
            try
            {
                var bb = el.get_BoundingBox(view);
                if (bb != null) return bb;
            }
            catch { }
            try { return el.get_BoundingBox(null); }
            catch { return null; }
        }

        private class GroupRef
        {
            public long GroupId { get; set; }
            public string GroupName { get; set; }
        }

        private class KeyedElem
        {
            public Element Elem { get; set; }
            public string TypeKey { get; set; }
            public string PosKey { get; set; }
        }

        public string GetName() => "DedupDetailElementsInViewEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 25000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
