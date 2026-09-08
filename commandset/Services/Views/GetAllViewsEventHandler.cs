using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Views
{
    public class GetAllViewsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private Document _doc => _uiApp.ActiveUIDocument.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        private string _viewTypeFilter;
        private string _levelNameFilter;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(string viewTypeFilter, string levelNameFilter)
        {
            _viewTypeFilter = viewTypeFilter;
            _levelNameFilter = levelNameFilter;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                var views = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted)
                    .Select(v =>
                    {
                        string levelName = v.GenLevel != null ? v.GenLevel.Name : string.Empty;
                        return new
                        {
                            ElementId = v.Id.GetIntValue(),
                            Name = v.Name,
                            ViewType = v.ViewType.ToString(),
                            LevelName = levelName,
                            Scale = v.Scale
                        };
                    })
                    .Where(v => string.IsNullOrEmpty(_viewTypeFilter) ||
                                v.ViewType.IndexOf(_viewTypeFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Where(v => string.IsNullOrEmpty(_levelNameFilter) ||
                                v.LevelName.IndexOf(_levelNameFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(v => v.ViewType)
                    .ThenBy(v => v.Name)
                    .ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully retrieved {views.Count} views",
                    Response = new
                    {
                        Count = views.Count,
                        Views = views
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error retrieving views: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "GetAllViewsEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
