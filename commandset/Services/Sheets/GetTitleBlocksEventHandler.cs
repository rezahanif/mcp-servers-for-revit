using RevitMCPCommandSet.Utils;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Sheets
{
    /// <summary>
    /// Event handler for listing all title block types in the project
    /// </summary>
    public class GetTitleBlocksEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private Document _doc => _uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<GetTitleBlocksResult> Result { get; private set; }

        public void SetParameters()
        {
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                var titleBlocks = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .Cast<FamilySymbol>()
                    .Select(fs => new TitleBlockSummary
                    {
                        ElementId = fs.Id.GetIntValue(),
                        Name = fs.Name,
                        FamilyName = fs.FamilyName
                    })
                    .OrderBy(t => t.FamilyName)
                    .ThenBy(t => t.Name)
                    .ToList();

                Result = new AIResult<GetTitleBlocksResult>
                {
                    Success = true,
                    Message = $"Found {titleBlocks.Count} title block type(s)",
                    Response = new GetTitleBlocksResult { Count = titleBlocks.Count, TitleBlocks = titleBlocks }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<GetTitleBlocksResult>
                {
                    Success = false,
                    Message = $"Error getting title blocks: {ex.Message}",
                };
                System.Diagnostics.Trace.WriteLine($"Error getting title blocks: {ex.Message}", "Error");
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
            return "Get Title Blocks";
        }
    }

    public class TitleBlockSummary
    {
        public int ElementId { get; set; }
        public string Name { get; set; }
        public string FamilyName { get; set; }
    }

    public class GetTitleBlocksResult
    {
        public int Count { get; set; }
        public List<TitleBlockSummary> TitleBlocks { get; set; }
    }
}
