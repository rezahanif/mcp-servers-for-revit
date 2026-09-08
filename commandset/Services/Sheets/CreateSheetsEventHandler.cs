using RevitMCPCommandSet.Utils;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.Sheets;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Sheets
{
    /// <summary>
    /// Event handler for batch-creating blank sheets
    /// </summary>
    public class CreateSheetsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private Document _doc => _uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        private int _titleBlockId;
        private List<SheetSpec> _sheets;

        public AIResult<CreateSheetsResult> Result { get; private set; }

        public void SetParameters(int titleBlockId, List<SheetSpec> sheets)
        {
            _titleBlockId = titleBlockId;
            _sheets = sheets;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                ElementId tbId = new ElementId(_titleBlockId);
                Element tbType = _doc.GetElement(tbId);
                if (tbType == null)
                    throw new Exception($"Title block type not found: {_titleBlockId}");

                var results = new List<CreateSheetResultItem>();

                using (Transaction trans = new Transaction(_doc, "Create Sheets"))
                {
                    trans.Start();

                    foreach (var s in _sheets)
                    {
                        try
                        {
                            ViewSheet sheet = ViewSheet.Create(_doc, tbId);
                            if (!string.IsNullOrEmpty(s.Number))
                                sheet.SheetNumber = s.Number;
                            if (!string.IsNullOrEmpty(s.Name))
                                sheet.Name = s.Name;

                            results.Add(new CreateSheetResultItem
                            {
                                ElementId = sheet.Id.GetIntValue(),
                                SheetNumber = sheet.SheetNumber,
                                SheetName = sheet.Name,
                                Success = true
                            });
                        }
                        catch (Exception ex)
                        {
                            results.Add(new CreateSheetResultItem
                            {
                                SheetNumber = s.Number,
                                SheetName = s.Name,
                                Success = false,
                                Error = ex.Message
                            });
                        }
                    }

                    trans.Commit();
                }

                int okCount = results.Count(r => r.Success);
                Result = new AIResult<CreateSheetsResult>
                {
                    Success = true,
                    Message = $"Created {okCount} of {_sheets.Count} sheet(s)",
                    Response = new CreateSheetsResult { Total = _sheets.Count, Results = results }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<CreateSheetsResult>
                {
                    Success = false,
                    Message = $"Error creating sheets: {ex.Message}",
                };
                System.Diagnostics.Trace.WriteLine($"Error creating sheets: {ex.Message}", "Error");
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
            return "Create Sheets";
        }
    }

    public class CreateSheetResultItem
    {
        public int? ElementId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class CreateSheetsResult
    {
        public int Total { get; set; }
        public List<CreateSheetResultItem> Results { get; set; }
    }
}
