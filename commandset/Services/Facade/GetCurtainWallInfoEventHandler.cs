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
    public class GetCurtainWallInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private int? _elementId;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(int? elementId)
        {
            _elementId = elementId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                Wall wall = null;
                if (_elementId.HasValue)
                {
                    Element elem = doc.GetElement(new ElementId(_elementId.Value));
                    wall = elem as Wall;
                }
                else
                {
                    var selection = uidoc.Selection.GetElementIds();
                    if (selection.Count == 0)
                        throw new Exception("Please select a curtain wall or specify elementId");

                    Element elem = doc.GetElement(selection.First());
                    wall = elem as Wall;
                }

                if (wall == null)
                    throw new Exception("Selected element is not a wall");

                CurtainGrid grid = wall.CurtainGrid;
                if (grid == null)
                    throw new Exception("The wall is not a curtain wall (no CurtainGrid)");

                var uGridIds = grid.GetUGridLineIds();
                var vGridIds = grid.GetVGridLineIds();
                var panelIds = grid.GetPanelIds();

                int rows = uGridIds.Count + 1;
                int columns = vGridIds.Count + 1;

                var panelTypeDict = new Dictionary<int, (string TypeName, string MaterialName, string MaterialColor, int Count)>();

                foreach (ElementId panelId in panelIds)
                {
                    Element panel = doc.GetElement(panelId);
                    if (panel == null) continue;

                    ElementId typeId = panel.GetTypeId();
                    int typeIdInt = typeId.GetIntValue();

                    if (!panelTypeDict.ContainsKey(typeIdInt))
                    {
                        ElementType panelType = doc.GetElement(typeId) as ElementType;
                        string typeName = panelType?.Name ?? "Unknown";

                        string materialName = "";
                        string materialColor = "#808080";

                        try
                        {
                            Parameter matParam = panelType?.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
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
                                }
                            }
                        }
                        catch { }

                        panelTypeDict[typeIdInt] = (typeName, materialName, materialColor, 0);
                    }

                    var current = panelTypeDict[typeIdInt];
                    panelTypeDict[typeIdInt] = (current.TypeName, current.MaterialName, current.MaterialColor, current.Count + 1);
                }

                double panelWidth = 0;
                double panelHeight = 0;
                if (panelIds.Count > 0)
                {
                    Element firstPanel = doc.GetElement(panelIds.First());
                    BoundingBoxXYZ bb = firstPanel?.get_BoundingBox(null);
                    if (bb != null)
                    {
                        panelWidth = Math.Round((bb.Max.X - bb.Min.X) * 304.8, 2);
                        panelHeight = Math.Round((bb.Max.Z - bb.Min.Z) * 304.8, 2);
                    }
                }

                var panelTypes = panelTypeDict.Select(kvp => new
                {
                    TypeId = kvp.Key,
                    TypeName = kvp.Value.TypeName,
                    MaterialName = kvp.Value.MaterialName,
                    MaterialColor = kvp.Value.MaterialColor,
                    Count = kvp.Value.Count
                }).ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Curtain wall info retrieved: {panelIds.Count} panel(s), {rows} row(s), {columns} column(s)",
                    Response = new
                    {
                        ElementId = wall.Id.GetIntValue(),
                        WallType = wall.WallType.Name,
                        IsCurtainWall = true,
                        Rows = rows,
                        Columns = columns,
                        TotalPanels = panelIds.Count,
                        PanelWidth = panelWidth,
                        PanelHeight = panelHeight,
                        UGridCount = uGridIds.Count,
                        VGridCount = vGridIds.Count,
                        PanelTypes = panelTypes,
                        PanelIds = panelIds.Select(id => id.GetIntValue()).ToList()
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error getting curtain wall info: {ex.Message}"
                };
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

        public string GetName() => "GetCurtainWallInfoEventHandler";
    }
}
