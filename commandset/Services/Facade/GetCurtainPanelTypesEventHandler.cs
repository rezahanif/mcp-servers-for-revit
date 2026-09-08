using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Facade
{
    public class GetCurtainPanelTypesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
                Document doc = _uiApp.ActiveUIDocument.Document;

                var panelTypes = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                    .WhereElementIsElementType()
                    .Cast<ElementType>()
                    .Select(pt =>
                    {
                        string materialName = "";
                        string materialColor = "#808080";
                        int transparency = 0;

                        try
                        {
                            Parameter matParam = pt.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                            if (matParam != null && matParam.HasValue)
                            {
                                ElementId matId = matParam.AsElementId();
                                Material mat = doc.GetElement(matId) as Material;
                                if (mat != null)
                                {
                                    materialName = mat.Name;
                                    Color color = mat.Color;
                                    if (color != null && color.IsValid)
                                    {
                                        materialColor = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
                                    }
                                    transparency = mat.Transparency;
                                }
                            }
                        }
                        catch { }

                        return new
                        {
                            TypeId = pt.Id.GetIntValue(),
                            TypeName = pt.Name,
                            Family = (pt as FamilySymbol)?.FamilyName ?? "System Panel",
                            MaterialName = materialName,
                            MaterialColor = materialColor,
                            Transparency = transparency
                        };
                    })
                    .OrderBy(pt => pt.Family)
                    .ThenBy(pt => pt.TypeName)
                    .ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Retrieved {panelTypes.Count} curtain panel types",
                    Response = new
                    {
                        Count = panelTypes.Count,
                        PanelTypes = panelTypes
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error getting curtain panel types: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "GetCurtainPanelTypesEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
