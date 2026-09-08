using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Views
{
    public class GetActiveViewEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<object> Result { get; private set; }

        public void SetParameters()
        {
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                View activeView = _uiApp.ActiveUIDocument.ActiveView;
                string levelName = activeView.GenLevel != null ? activeView.GenLevel.Name : string.Empty;

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Active view: {activeView.Name}",
                    Response = new
                    {
                        ElementId = activeView.Id.GetIntValue(),
                        Name = activeView.Name,
                        ViewType = activeView.ViewType.ToString(),
                        LevelName = levelName,
                        Scale = activeView.Scale
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error getting active view: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "GetActiveViewEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
