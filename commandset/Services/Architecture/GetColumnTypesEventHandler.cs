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
    /// Event handler for reading available column types (architectural + structural)
    /// </summary>
    public class GetColumnTypesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private string _materialFilter;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(string materialFilter)
        {
            _materialFilter = materialFilter;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                var columnTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(fs => fs.Category != null &&
                        (fs.Category.Id.GetIntValue() == (int)BuiltInCategory.OST_Columns ||
                         fs.Category.Id.GetIntValue() == (int)BuiltInCategory.OST_StructuralColumns))
                    .Select(fs =>
                    {
                        double width = 0, depth = 0;
                        Parameter widthParam = fs.LookupParameter("Width") ?? fs.LookupParameter("b");
                        Parameter depthParam = fs.LookupParameter("Depth") ?? fs.LookupParameter("h");

                        if (widthParam != null && widthParam.HasValue)
                            width = System.Math.Round(widthParam.AsDouble() * 304.8, 0);
                        if (depthParam != null && depthParam.HasValue)
                            depth = System.Math.Round(depthParam.AsDouble() * 304.8, 0);

                        return new
                        {
                            ElementId = fs.Id.GetIntValue(),
                            TypeName = fs.Name,
                            FamilyName = fs.FamilyName,
                            Category = fs.Category?.Name,
                            Width = width,
                            Depth = depth,
                            SizeDescription = width > 0 && depth > 0 ? $"{width}x{depth}" : "unknown size",
                        };
                    })
                    .Where(ct => string.IsNullOrEmpty(_materialFilter) ||
                                 ct.FamilyName.Contains(_materialFilter) ||
                                 ct.TypeName.Contains(_materialFilter))
                    .OrderBy(ct => ct.FamilyName)
                    .ThenBy(ct => ct.TypeName)
                    .ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Found {columnTypes.Count} column type(s)",
                    Response = new { Count = columnTypes.Count, ColumnTypes = columnTypes },
                };
            }
            catch (System.Exception ex)
            {
                Result = new AIResult<object> { Success = false, Message = $"Error getting column types: {ex.Message}" };
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

        public string GetName() => "Get Column Types";
    }
}
