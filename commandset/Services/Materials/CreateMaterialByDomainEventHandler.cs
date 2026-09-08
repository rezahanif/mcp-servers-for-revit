using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Materials
{
    public class CreateMaterialByDomainEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private Document _doc => _uiApp.ActiveUIDocument.Document;
        private readonly System.Threading.ManualResetEvent _resetEvent = new System.Threading.ManualResetEvent(false);
        private string _materialName;
        private bool _dryRun;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(string materialName, bool dryRun)
        {
            _materialName = materialName;
            _dryRun = dryRun;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;
            try
            {
                Document doc = _doc;
                bool exists = MaterialHelpers.MaterialExistsByName(doc, _materialName);

                if (_dryRun)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = exists
                            ? $"[dryRun] Material '{_materialName}' already exists"
                            : $"[dryRun] Material '{_materialName}' would be created",
                        Response = new { DryRun = true, AlreadyExists = exists, PlannedMaterialName = _materialName },
                    };
                    return;
                }

                using (Transaction trans = new Transaction(doc, $"Create pure material: {_materialName}"))
                {
                    trans.Start();
                    Material mat = MaterialHelpers.GetOrCreatePureMaterial(doc, _materialName, new Color(235, 245, 240));
                    trans.Commit();

                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"Created material '{mat.Name}' (ID: {mat.Id.GetIntValue()})",
                        Response = new { MaterialId = mat.Id.GetIntValue(), MaterialName = mat.Name },
                    };
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object> { Success = false, Message = $"Error: {ex.Message}" };
            }
            finally { _resetEvent.Set(); }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000) => _resetEvent.WaitOne(timeoutMilliseconds);
        public string GetName() => "Create Material By Domain";
    }
}
