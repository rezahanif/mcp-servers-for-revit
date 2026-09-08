using System;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Views
{
    public class RenameViewEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private long _viewId;
        private string _newName;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(long viewId, string newName)
        {
            _viewId = viewId;
            _newName = newName;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                Element elem = doc.GetElement(new ElementId(_viewId));
                if (elem == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = $"Element not found with ID: {_viewId}"
                    };
                    return;
                }

                View view = elem as View;
                if (view == null)
                {
                    string viewName = elem.Name;
                    view = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .FirstOrDefault(v => v.Name == viewName);
                }

                if (view == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = $"Could not resolve view for ID: {_viewId}"
                    };
                    return;
                }

                using (Transaction trans = new Transaction(doc, "Rename View"))
                {
                    trans.Start();
                    view.Name = _newName;
                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully renamed view to: {_newName}",
                    Response = new
                    {
                        ViewId = _viewId,
                        NewName = _newName
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error renaming view: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "RenameViewEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
