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
    /// Event handler for reading all grid lines
    /// </summary>
    public class GetAllGridsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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

                var grids = new FilteredElementCollector(doc)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .Select(g =>
                    {
                        Curve curve = g.Curve;
                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        double dx = System.Math.Abs(end.X - start.X);
                        double dy = System.Math.Abs(end.Y - start.Y);
                        string direction = dx > dy ? "Horizontal" : "Vertical";

                        return new
                        {
                            ElementId = g.Id.GetIntValue(),
                            Name = g.Name,
                            Direction = direction,
                            StartX = System.Math.Round(start.X * 304.8, 2),
                            StartY = System.Math.Round(start.Y * 304.8, 2),
                            EndX = System.Math.Round(end.X * 304.8, 2),
                            EndY = System.Math.Round(end.Y * 304.8, 2),
                        };
                    })
                    .OrderBy(g => g.Name)
                    .ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Found {grids.Count} grid line(s)",
                    Response = new { Count = grids.Count, Grids = grids },
                };
            }
            catch (System.Exception ex)
            {
                Result = new AIResult<object> { Success = false, Message = $"Error getting grids: {ex.Message}" };
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

        public string GetName() => "Get All Grids";
    }
}
