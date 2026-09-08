using System.Collections.Generic;
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
    /// Event handler for reading available line styles (GraphicsStyles under the Lines subcategory)
    /// </summary>
    public class GetLineStylesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<object> Result { get; private set; }

        public void SetParameters()
        {
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;
                var result = new List<Dictionary<string, object>>();

                var allStyles = new FilteredElementCollector(doc)
                    .OfClass(typeof(GraphicsStyle))
                    .ToElements();

                foreach (Element elem in allStyles)
                {
                    if (elem is not GraphicsStyle gs) continue;

                    try
                    {
                        Category cat = gs.GraphicsStyleCategory;
                        if (cat == null) continue;
                        Category parent = cat.Parent;
                        if (parent == null) continue;

                        if (parent.Name == "Lines")
                        {
                            result.Add(new Dictionary<string, object>
                            {
                                { "Id", gs.Id.GetIntValue() },
                                { "Name", gs.Name },
                            });
                        }
                    }
                    catch
                    {
                        // Some GraphicsStyles throw reading Category/Parent — skip them, not fatal.
                    }
                }

                var sorted = result.OrderBy(r => r["Name"].ToString()).ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Found {sorted.Count} line style(s)",
                    Response = sorted,
                };
            }
            catch (System.Exception ex)
            {
                Result = new AIResult<object> { Success = false, Message = $"Error getting line styles: {ex.Message}" };
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

        public string GetName() => "Get Line Styles";
    }
}
