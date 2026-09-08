using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    /// <summary>
    /// Event handler for listing wall types
    /// </summary>
    public class GetWallTypesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private string _search;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(string search)
        {
            _search = search;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                var wallTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .Select(wt => new { ElementId = wt.Id.GetIntValue(), Name = wt.Name })
                    .AsEnumerable();

                if (!string.IsNullOrEmpty(_search))
                    wallTypes = wallTypes.Where(wt => wt.Name.Contains(_search));

                var result = wallTypes.OrderBy(wt => wt.Name).ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Found {result.Count} wall type(s)",
                    Response = new { Count = result.Count, WallTypes = result },
                };
            }
            catch (System.Exception ex)
            {
                Result = new AIResult<object> { Success = false, Message = $"Error getting wall types: {ex.Message}" };
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

        public string GetName() => "Get Wall Types";
    }
}
