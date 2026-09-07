using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System;
using System.IO;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class ExportNavisworksEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string OutputFolder { get; set; }
        public string FileName { get; set; }
        public bool ExportElementIds { get; set; } = true;
        public bool ConvertLinkedCAD { get; set; } = true;

        public AIResult<string> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 30000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Export Navisworks";

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<string> { Success = false, Message = "No active Revit document.", Response = null };
                    return;
                }

                string folder = string.IsNullOrEmpty(OutputFolder) ? Path.GetTempPath() : OutputFolder;
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string name = string.IsNullOrEmpty(FileName) ? (string.IsNullOrEmpty(doc.Title) ? "Model" : doc.Title) : FileName;
                if (name.EndsWith(".nwc", StringComparison.OrdinalIgnoreCase))
                {
                    name = Path.GetFileNameWithoutExtension(name);
                }

                var options = new NavisworksExportOptions
                {
                    ExportScope = NavisworksExportScope.Model,
                    ExportElementIds = ExportElementIds,
                    ConvertLinkedCADFormats = ConvertLinkedCAD,
                    Coordinates = NavisworksCoordinates.Shared
                };

                doc.Export(folder, name, options);
                string expectedPath = Path.Combine(folder, name + ".nwc");

                Result = new AIResult<string>
                {
                    Success = true,
                    Message = $"Successfully exported Navisworks coordination file: {expectedPath}",
                    Response = expectedPath
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<string>
                {
                    Success = false,
                    Message = $"Error exporting to Navisworks: {ex.Message}",
                    Response = null
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
