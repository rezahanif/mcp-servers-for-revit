using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Views
{
    public class SetActiveViewEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private long _viewId;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(long viewId)
        {
            _viewId = viewId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                View view = doc.GetElement(new ElementId(_viewId)) as View;
                if (view == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = $"View not found with ID: {_viewId}"
                    };
                    return;
                }

                _uiApp.ActiveUIDocument.ActiveView = view;

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully switched to view: {view.Name}",
                    Response = new
                    {
                        ViewId = _viewId,
                        ViewName = view.Name
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error setting active view: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "SetActiveViewEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
