using RevitMCPCommandSet.Utils;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Sheets
{
    /// <summary>
    /// Event handler for listing all sheets in the project
    /// </summary>
    public class GetAllSheetsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private Document _doc => _uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<GetAllSheetsResult> Result { get; private set; }

        public void SetParameters()
        {
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                var sheets = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Select(s => new SheetSummary
                    {
                        ElementId = s.Id.GetIntValue(),
                        SheetNumber = s.SheetNumber,
                        SheetName = s.Name
                    })
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                Result = new AIResult<GetAllSheetsResult>
                {
                    Success = true,
                    Message = $"Found {sheets.Count} sheet(s)",
                    Response = new GetAllSheetsResult { Count = sheets.Count, Sheets = sheets }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<GetAllSheetsResult>
                {
                    Success = false,
                    Message = $"Error getting sheets: {ex.Message}",
                };
                System.Diagnostics.Trace.WriteLine($"Error getting sheets: {ex.Message}", "Error");
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
            return "Get All Sheets";
        }
    }

    public class SheetSummary
    {
        public int ElementId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
    }

    public class GetAllSheetsResult
    {
        public int Count { get; set; }
        public List<SheetSummary> Sheets { get; set; }
    }
}
