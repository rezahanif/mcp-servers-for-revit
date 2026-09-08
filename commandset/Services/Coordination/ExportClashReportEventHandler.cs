using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Coordination
{
    public class ExportClashReportEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                JObject clashData = _parameters["clashData"] as JObject;
                string format = _parameters["format"]?.Value<string>() ?? "csv";
                string outputPath = _parameters["outputPath"]?.Value<string>();
                string reportTitle = _parameters["reportTitle"]?.Value<string>() ?? "Clash Detection Report";

                if (clashData == null)
                    throw new ArgumentException("clashData is required");

                if (string.IsNullOrEmpty(outputPath))
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    outputPath = Path.Combine(desktop, $"clash_report_{timestamp}");
                }

                JArray clashes = (clashData["Response"] as JArray) ?? (clashData["Clashes"] as JArray);
                if (clashes == null || clashes.Count == 0)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = "No clashes provided in clashData"
                    };
                    return;
                }

                var outputPaths = new List<string>();

                if (format == "csv" || format == "both")
                {
                    string csvPath = outputPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? outputPath : outputPath + ".csv";
                    var sb = new StringBuilder();
                    sb.AppendLine($"# {reportTitle}");
                    sb.AppendLine("Index,ElementIdA,NameA,CategoryA,ElementIdB,NameB,CategoryB,ClashType,Recommendation");

                    int idx = 1;
                    foreach (var clash in clashes)
                    {
                        long idA = clash["ElementIdA"]?.Value<long>() ?? clash["CsaElement"]?["Id"]?.Value<long>() ?? 0;
                        string nameA = EscapeCsv(clash["NameA"]?.Value<string>() ?? clash["CsaElement"]?["Name"]?.Value<string>() ?? "");
                        string catA = EscapeCsv(clash["CategoryA"]?.Value<string>() ?? clash["CsaElement"]?["Category"]?.Value<string>() ?? "");

                        long idB = clash["ElementIdB"]?.Value<long>() ?? clash["MepElement"]?["Id"]?.Value<long>() ?? 0;
                        string nameB = EscapeCsv(clash["NameB"]?.Value<string>() ?? clash["MepElement"]?["Name"]?.Value<string>() ?? "");
                        string catB = EscapeCsv(clash["CategoryB"]?.Value<string>() ?? clash["MepElement"]?["Category"]?.Value<string>() ?? "");

                        string clashType = EscapeCsv(clash["ClashType"]?.Value<string>() ?? "HardClash");
                        string rec = EscapeCsv(clash["Recommendation"]?.Value<string>() ?? "");

                        sb.AppendLine($"{idx++},{idA},{nameA},{catA},{idB},{nameB},{catB},{clashType},{rec}");
                    }

                    File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
                    outputPaths.Add(csvPath);
                }

                if (format == "json" || format == "both")
                {
                    string jsonPath = outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? outputPath : outputPath + ".json";
                    var reportObj = new
                    {
                        ReportTitle = reportTitle,
                        GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        TotalClashes = clashes.Count,
                        Clashes = clashes
                    };
                    File.WriteAllText(jsonPath, JsonConvert.SerializeObject(reportObj, Formatting.Indented), Encoding.UTF8);
                    outputPaths.Add(jsonPath);
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully exported clash report to {string.Join(", ", outputPaths)}",
                    Response = new
                    {
                        RowCount = clashes.Count,
                        OutputPaths = outputPaths
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error exporting clash report: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0)
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        public string GetName() => "ExportClashReportEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
