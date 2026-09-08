using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    /// <summary>
    /// Event handler for getting all levels in the project
    /// </summary>
    public class GetAllLevelsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private Document _doc => _uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<object> Result { get; private set; }

        public void SetParameters()
        {
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(l => new
                    {
                        ElementId = l.Id.GetIntValue(),
                        Name = l.Name,
                        Elevation = Math.Round(l.Elevation * 304.8, 2) // to mm
                    })
                    .ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Found {levels.Count} level(s)",
                    Response = new { Count = levels.Count, Levels = levels }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error getting levels: {ex.Message}"
                };
                System.Diagnostics.Trace.WriteLine($"Error getting levels: {ex.Message}", "Error");
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Get All Levels";
        }
    }
}
