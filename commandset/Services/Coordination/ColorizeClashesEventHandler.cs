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

namespace RevitMCPCommandSet.Services.Coordination
{
    public class ColorizeClashesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                JObject clashData = _parameters["clashData"] as JObject;
                string colorScheme = _parameters["colorScheme"]?.Value<string>() ?? "by_category_b";
                long? viewIdParam = _parameters["viewId"]?.Value<long?>();

                if (clashData == null)
                    throw new ArgumentException("clashData is required");

                View view;
                if (viewIdParam.HasValue && viewIdParam.Value > 0)
                {
#if REVIT2024_OR_GREATER
                    view = doc.GetElement(new ElementId(viewIdParam.Value)) as View;
#else
                    view = doc.GetElement(new ElementId((int)viewIdParam.Value)) as View;
#endif
                    if (view == null) throw new InvalidOperationException($"View not found with ID: {viewIdParam.Value}");
                }
                else
                {
                    view = doc.ActiveView;
                }

                JArray clashes = (clashData["Response"] as JArray) ?? (clashData["Clashes"] as JArray);
                if (clashes == null || clashes.Count == 0)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = "No clashes provided to colorize"
                    };
                    return;
                }

                ElementId solidPatternId = GetSolidFillPatternId(doc);
                int coloredCount = 0;
                var colorPalette = new List<Color>
                {
                    new Color(255, 60, 60),
                    new Color(255, 140, 0),
                    new Color(255, 215, 0),
                    new Color(50, 205, 50),
                    new Color(30, 144, 255),
                    new Color(148, 0, 211),
                    new Color(255, 20, 147)
                };

                var keyToColor = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

                using (Transaction trans = new Transaction(doc, "Colorize Clashes"))
                {
                    trans.Start();

                    foreach (var clash in clashes)
                    {
                        long idA = clash["ElementIdA"]?.Value<long>() ?? clash["CsaElement"]?["Id"]?.Value<long>() ?? 0;
                        long idB = clash["ElementIdB"]?.Value<long>() ?? clash["MepElement"]?["Id"]?.Value<long>() ?? 0;

                        string key = colorScheme == "by_clash_type"
                            ? (clash["ClashType"]?.Value<string>() ?? "HardClash")
                            : (clash["CategoryB"]?.Value<string>() ?? clash["MepElement"]?["SystemType"]?.Value<string>() ?? "CategoryB");

                        if (!keyToColor.TryGetValue(key, out var color))
                        {
                            color = colorPalette[keyToColor.Count % colorPalette.Count];
                            keyToColor[key] = color;
                        }

                        var overrideSettings = new OverrideGraphicSettings();
                        overrideSettings.SetSurfaceForegroundPatternColor(color);
                        if (solidPatternId != ElementId.InvalidElementId)
                        {
                            overrideSettings.SetSurfaceForegroundPatternId(solidPatternId);
                            overrideSettings.SetSurfaceForegroundPatternVisible(true);
                        }
                        overrideSettings.SetCutForegroundPatternColor(color);
                        if (solidPatternId != ElementId.InvalidElementId)
                        {
                            overrideSettings.SetCutForegroundPatternId(solidPatternId);
                            overrideSettings.SetCutForegroundPatternVisible(true);
                        }
                        overrideSettings.SetProjectionLineColor(color);
                        overrideSettings.SetCutLineColor(color);

                        if (idA > 0)
                        {
#if REVIT2024_OR_GREATER
                            view.SetElementOverrides(new ElementId(idA), overrideSettings);
#else
                            view.SetElementOverrides(new ElementId((int)idA), overrideSettings);
#endif
                            coloredCount++;
                        }
                        if (idB > 0)
                        {
#if REVIT2024_OR_GREATER
                            view.SetElementOverrides(new ElementId(idB), overrideSettings);
#else
                            view.SetElementOverrides(new ElementId((int)idB), overrideSettings);
#endif
                            coloredCount++;
                        }
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully applied clash color overrides to {coloredCount} elements",
                    Response = new
                    {
                        ColoredCount = coloredCount,
                        ColorScheme = colorScheme,
                        ViewName = view.Name
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error colorizing clashes: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private static ElementId GetSolidFillPatternId(Document doc)
        {
            try
            {
                var fillPattern = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);
                return fillPattern?.Id ?? ElementId.InvalidElementId;
            }
            catch
            {
                return ElementId.InvalidElementId;
            }
        }

        public string GetName() => "ColorizeClashesEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
