using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    /// <summary>
    /// Event handler for getting basic project information
    /// </summary>
    public class GetProjectInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private UIDocument _uiDoc => _uiApp.ActiveUIDocument;
        private Document _doc => _uiDoc.Document;

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
                ProjectInfo projInfo = _doc.ProjectInformation;

                var info = new
                {
                    ProjectName = _doc.Title,
                    BuildingName = projInfo.BuildingName,
                    OrganizationName = projInfo.OrganizationName,
                    Author = projInfo.Author,
                    Address = projInfo.Address,
                    ClientName = projInfo.ClientName,
                    ProjectNumber = projInfo.Number,
                    ProjectStatus = projInfo.Status
                };

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = "Successfully retrieved project info",
                    Response = info
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error getting project info: {ex.Message}"
                };
                System.Diagnostics.Trace.WriteLine($"Error getting project info: {ex.Message}", "Error");
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
            return "Get Project Info";
        }
    }
}
