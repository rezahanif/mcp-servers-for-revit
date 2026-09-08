using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Materials
{
    /// <summary>
    /// Ported from REVIT_MCP_study's CommandExecutor.Material.cs CreateCustomMaterial:
    /// reuses an existing material as a base via Duplicate() when available, otherwise
    /// Material.Create(); sets a light default color and MaterialClass; duplicates a
    /// sample AppearanceAssetElement so the new material has its own independent asset.
    /// </summary>
    public class CreateMaterialEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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

                if (_dryRun)
                {
                    Material existing = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                        .FirstOrDefault(m => m.Name.Equals(_materialName, StringComparison.OrdinalIgnoreCase));
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = existing != null
                            ? $"[dryRun] Material '{existing.Name}' already exists (ID:{existing.Id.GetIntValue()})"
                            : $"[dryRun] Material '{_materialName}' does not exist yet; would be created",
                        Response = new
                        {
                            DryRun = true,
                            AlreadyExists = existing != null,
                            MaterialId = existing?.Id.GetIntValue(),
                            MaterialName = existing?.Name ?? _materialName,
                        },
                    };
                    return;
                }

                using (Transaction trans = new Transaction(doc, $"Create material: {_materialName}"))
                {
                    trans.Start();

                    FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Material));
                    Material found = collector.Cast<Material>()
                        .FirstOrDefault(m => m.Name.Equals(_materialName, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        trans.Commit();
                        Result = new AIResult<object>
                        {
                            Success = true,
                            Message = $"Material '{found.Name}' already exists (ID: {found.Id.GetIntValue()})",
                            Response = new { MaterialId = found.Id.GetIntValue(), MaterialName = found.Name },
                        };
                        return;
                    }

                    Material baseMat = collector.Cast<Material>()
                        .FirstOrDefault(m => m.Name.Contains("Default") || m.Name.Contains("預設"))
                        ?? collector.Cast<Material>().FirstOrDefault();

                    Material newMat = null;
                    if (baseMat != null)
                    {
                        newMat = baseMat.Duplicate(_materialName);
                    }
                    else
                    {
                        try
                        {
                            ElementId matId = Material.Create(doc, _materialName);
                            newMat = doc.GetElement(matId) as Material;
                        }
                        catch { /* fall through to null check below */ }
                    }

                    if (newMat == null)
                        throw new Exception($"Failed to create or duplicate material '{_materialName}'");

                    newMat.Color = new Color(200, 220, 240);
                    newMat.MaterialClass = "Test";

                    var sampleAsset = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.AppearanceAssetElement))
                        .Cast<Autodesk.Revit.DB.AppearanceAssetElement>().FirstOrDefault();
                    if (sampleAsset != null)
                    {
                        try
                        {
                            string uniqueAssetName = MaterialHelpers.GenerateUniqueAssetName(doc, _materialName + "_Asset");
                            var newAsset = sampleAsset.Duplicate(uniqueAssetName);
                            newMat.AppearanceAssetId = newAsset.Id;
                        }
                        catch { /* best-effort; independent asset is a nice-to-have, not required */ }
                    }

                    trans.Commit();

                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"Created material '{newMat.Name}' (ID: {newMat.Id.GetIntValue()})",
                        Response = new { MaterialId = newMat.Id.GetIntValue(), MaterialName = newMat.Name },
                    };
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object> { Success = false, Message = $"Error creating material: {ex.Message}" };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Create Material";
    }
}
