using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Materials
{
    public class CreateGreenMaterialEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private Document _doc => _uiApp.ActiveUIDocument.Document;
        private readonly System.Threading.ManualResetEvent _resetEvent = new System.Threading.ManualResetEvent(false);
        private string _materialName;
        private int _r, _g, _b;
        private bool _dryRun;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(string materialName, int r, int g, int b, bool dryRun)
        {
            _materialName = materialName; _r = r; _g = g; _b = b; _dryRun = dryRun;
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
                            : $"[dryRun] Material '{_materialName}' would be created with color #{_r:X2}{_g:X2}{_b:X2}",
                        Response = new { DryRun = true, AlreadyExists = exists, PlannedMaterialName = _materialName, PlannedColor = new { r = _r, g = _g, b = _b } },
                    };
                    return;
                }

                using (Transaction trans = new Transaction(doc, $"Create green material: {_materialName}"))
                {
                    trans.Start();
                    Material mat = MaterialHelpers.GetOrCreatePureMaterial(doc, _materialName, new Color((byte)_r, (byte)_g, (byte)_b));
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
        public string GetName() => "Create Green Material";
    }
}
