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
    /// Event handler for listing loaded furniture types
    /// </summary>
    public class GetFurnitureTypesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private string _categoryFilter;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(string categoryFilter)
        {
            _categoryFilter = categoryFilter;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                var furnitureTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_Furniture)
                    .Cast<FamilySymbol>()
                    .Select(fs => new
                    {
                        ElementId = fs.Id.GetIntValue(),
                        TypeName = fs.Name,
                        FamilyName = fs.FamilyName,
                        IsActive = fs.IsActive,
                    })
                    .Where(ft => string.IsNullOrEmpty(_categoryFilter) ||
                                 ft.FamilyName.Contains(_categoryFilter) ||
                                 ft.TypeName.Contains(_categoryFilter))
                    .OrderBy(ft => ft.FamilyName)
                    .ThenBy(ft => ft.TypeName)
                    .ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Found {furnitureTypes.Count} furniture type(s)",
                    Response = new { Count = furnitureTypes.Count, FurnitureTypes = furnitureTypes },
                };
            }
            catch (System.Exception ex)
            {
                Result = new AIResult<object> { Success = false, Message = $"Error getting furniture types: {ex.Message}" };
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

        public string GetName() => "Get Furniture Types";
    }
}
