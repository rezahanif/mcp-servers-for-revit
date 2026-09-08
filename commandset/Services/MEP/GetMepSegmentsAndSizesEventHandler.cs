using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.MEP
{
    public class GetMepSegmentsAndSizesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
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
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                bool usedOnly = _parameters?["usedOnly"]?.Value<bool?>() ?? false;
                bool summaryOnly = _parameters?["summaryOnly"]?.Value<bool?>() ?? false;
                string segmentName = _parameters?["segmentName"]?.Value<string>()?.Trim();
                bool hasNameFilter = !string.IsNullOrWhiteSpace(segmentName);
                bool includeDuct = _parameters?["includeDuct"]?.Value<bool?>() ?? !hasNameFilter;

                var segments = new List<object>();
                int totalPipeSizes = 0;
                int reportedPipeSizes = 0;

                var allPipeSegments = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipeSegment))
                    .Cast<PipeSegment>()
                    .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var pipeSegments = hasNameFilter
                    ? allPipeSegments.Where(s => s.Name != null &&
                        s.Name.IndexOf(segmentName, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
                    : allPipeSegments;

                foreach (PipeSegment seg in pipeSegments)
                {
                    var sizes = new List<object>();
                    int inSizeLists = 0;
                    int inSizing = 0;

                    foreach (MEPSize size in seg.GetSizes().OrderBy(s => s.NominalDiameter))
                    {
                        totalPipeSizes++;
                        if (size.UsedInSizeLists) inSizeLists++;
                        if (size.UsedInSizing) inSizing++;
                        if (usedOnly && !size.UsedInSizeLists && !size.UsedInSizing) continue;
                        reportedPipeSizes++;
                        if (!summaryOnly) sizes.Add(MepSettingsHelpers.DescribeSize(size));
                    }

                    segments.Add(new
                    {
                        id = seg.Id.GetIntValue(),
                        kind = "pipe",
                        name = seg.Name,
                        material = MepSettingsHelpers.GetElementNameOrNull(doc, seg.MaterialId),
                        schedule = MepSettingsHelpers.GetElementNameOrNull(doc, seg.ScheduleTypeId),
                        description = string.IsNullOrWhiteSpace(seg.Description) ? null : seg.Description,
                        roughness_mm = MepSettingsHelpers.ToMm(seg.Roughness, 6),
                        sizeCount = seg.SizeCount,
                        usedInSizeListsCount = inSizeLists,
                        usedInSizingCount = inSizing,
                        sizes = summaryOnly ? null : sizes,
                    });
                }

                var ductShapes = new List<object>();
                var ductRoundNominalMm = new List<double>();
                string ductNote = null;

                if (includeDuct)
                {
                    try
                    {
                        DuctSizeSettings ductSettings = DuctSizeSettings.GetDuctSizeSettings(doc);
                        if (ductSettings == null)
                        {
                            ductNote = "Document has no DuctSizeSettings (may not be an MEP template).";
                        }
                        else
                        {
                            foreach (DuctShape shape in new[] { DuctShape.Round, DuctShape.Rectangular, DuctShape.Oval })
                            {
                                DuctSizes ductSizes = ductSettings[shape];
                                if (ductSizes == null) continue;

                                var shapeSizes = new List<object>();
                                int inSizeLists = 0;
                                int inSizing = 0;

                                foreach (MEPSize size in MepSettingsHelpers.EnumerateDuctSizes(ductSizes).OrderBy(s => s.NominalDiameter))
                                {
                                    if (size.UsedInSizeLists) inSizeLists++;
                                    if (size.UsedInSizing) inSizing++;
                                    if (usedOnly && !size.UsedInSizeLists && !size.UsedInSizing) continue;
                                    if (!summaryOnly) shapeSizes.Add(MepSettingsHelpers.DescribeDuctSize(size));
                                    if (shape == DuctShape.Round)
                                        ductRoundNominalMm.Add(MepSettingsHelpers.ToMm(size.NominalDiameter));
                                }

                                ductShapes.Add(new
                                {
                                    shape = shape.ToString().ToLower(),
                                    sizeCount = shapeSizes.Count,
                                    usedInSizeListsCount = inSizeLists,
                                    usedInSizingCount = inSizing,
                                    sizes = summaryOnly ? null : shapeSizes,
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ductNote = $"Failed reading duct sizes: {ex.Message}";
                    }
                }

                string msg = MepSettingsHelpers.BuildMepInventoryMessage(
                    pipeSegments.Count, allPipeSegments.Count, totalPipeSizes, reportedPipeSizes,
                    usedOnly, summaryOnly, hasNameFilter);

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = msg,
                    Response = new
                    {
                        pipeSegments = segments,
                        ductShapes = ductShapes,
                        ductRoundNominalMm = ductRoundNominalMm,
                        ductNote = ductNote,
                        message = msg
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error getting MEP segments and sizes: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "GetMepSegmentsAndSizesEventHandler";
    }
}
