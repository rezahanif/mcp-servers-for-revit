using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    /// <summary>
    /// Event handler for moving an element by a mm displacement
    /// </summary>
    public class MoveElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private Document _doc => _uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        private int _elementId;
        private double _dx;
        private double _dy;
        private double _dz;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(int elementId, double dx, double dy, double dz)
        {
            _elementId = elementId;
            _dx = dx;
            _dy = dy;
            _dz = dz;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Element element = _doc.GetElement(new ElementId(_elementId));
                if (element == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = $"Element not found: {_elementId}"
                    };
                    return;
                }

                using (Transaction trans = new Transaction(_doc, $"Move element: {_elementId}"))
                {
                    trans.Start();

                    // mm -> feet
                    XYZ translation = new XYZ(_dx / 304.8, _dy / 304.8, _dz / 304.8);
                    ElementTransformUtils.MoveElement(_doc, new ElementId(_elementId), translation);

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = "Successfully moved element",
                    Response = new { ElementId = _elementId, Dx = _dx, Dy = _dy, Dz = _dz }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error moving element: {ex.Message}"
                };
                System.Diagnostics.Trace.WriteLine($"Error moving element: {ex.Message}", "Error");
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
            return "Move Element";
        }
    }
}
