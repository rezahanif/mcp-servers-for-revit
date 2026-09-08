using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Interaction
{
    public class ZoomToElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private long _elementId;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(long elementId)
        {
            _elementId = elementId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                Element elem = doc.GetElement(new ElementId(_elementId));
                if (elem == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = $"Element not found with ID: {_elementId}"
                    };
                    return;
                }

                _uiApp.ActiveUIDocument.ShowElements(new List<ElementId> { elem.Id });

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully zoomed to element: {elem.Name}",
                    Response = new
                    {
                        ElementId = _elementId,
                        ElementName = elem.Name
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error zooming to element: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "ZoomToElementEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
