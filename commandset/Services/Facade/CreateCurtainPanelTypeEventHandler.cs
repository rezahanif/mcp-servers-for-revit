using System;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Facade
{
    public class CreateCurtainPanelTypeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private JObject _parameters;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(JObject parameters)
        {
            _parameters = parameters;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                string typeName = _parameters["typeName"]?.Value<string>();
                string colorHex = _parameters["color"]?.Value<string>() ?? "#808080";
                long? baseTypeId = _parameters["baseTypeId"]?.Value<long?>();

                if (string.IsNullOrEmpty(typeName))
                    throw new ArgumentException("typeName is required");

                colorHex = colorHex.TrimStart('#');
                byte r = 128, g = 128, b = 128;
                if (colorHex.Length >= 6)
                {
                    r = Convert.ToByte(colorHex.Substring(0, 2), 16);
                    g = Convert.ToByte(colorHex.Substring(2, 2), 16);
                    b = Convert.ToByte(colorHex.Substring(4, 2), 16);
                }
                Color revitColor = new Color(r, g, b);

                using (Transaction trans = new Transaction(doc, "Create Curtain Panel Type"))
                {
                    trans.Start();

                    ElementType basePanelType = null;
                    if (baseTypeId.HasValue && baseTypeId.Value > 0)
                    {
#if REVIT2024_OR_GREATER
                        basePanelType = doc.GetElement(new ElementId(baseTypeId.Value)) as ElementType;
#else
                        basePanelType = doc.GetElement(new ElementId((int)baseTypeId.Value)) as ElementType;
#endif
                    }

                    if (basePanelType == null)
                    {
                        basePanelType = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                            .WhereElementIsElementType()
                            .Cast<ElementType>()
                            .FirstOrDefault();
                    }

                    if (basePanelType == null)
                        throw new InvalidOperationException("No curtain panel type available as base");

                    ElementType existingType = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                        .WhereElementIsElementType()
                        .Cast<ElementType>()
                        .FirstOrDefault(pt => pt.Name == typeName);

                    ElementType newPanelType;
                    bool isNew = false;
                    if (existingType != null)
                    {
                        newPanelType = existingType;
                    }
                    else
                    {
                        newPanelType = basePanelType.Duplicate(typeName) as ElementType;
                        isNew = true;
                    }

                    string materialName = $"CW_PNL_{typeName}";
                    Material material = new FilteredElementCollector(doc)
                        .OfClass(typeof(Material))
                        .Cast<Material>()
                        .FirstOrDefault(m => m.Name == materialName);

                    if (material == null)
                    {
                        ElementId newMatId = Material.Create(doc, materialName);
                        material = doc.GetElement(newMatId) as Material;
                    }

                    if (material != null)
                    {
                        material.Color = revitColor;
                        Parameter matParam = newPanelType.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                        if (matParam != null && !matParam.IsReadOnly)
                        {
                            matParam.Set(material.Id);
                        }
                    }

                    trans.Commit();

                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = isNew ? $"Successfully created panel type: {typeName}" : $"Updated panel type: {typeName}",
                        Response = new
                        {
                            TypeId = newPanelType.Id.GetIntValue(),
                            TypeName = typeName,
                            IsNewType = isNew,
                            MaterialId = material?.Id.GetIntValue() ?? 0,
                            MaterialName = materialName,
                            Color = $"#{r:X2}{g:X2}{b:X2}"
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error creating curtain panel type: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "CreateCurtainPanelTypeEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
