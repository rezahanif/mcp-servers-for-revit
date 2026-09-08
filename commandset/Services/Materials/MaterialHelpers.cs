using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services.Materials
{
    /// <summary>
    /// Shared Revit-API helpers for the Materials/Visualization tool batch.
    /// Ported from REVIT_MCP_study's CommandExecutor.Material.cs /
    /// CommandExecutor.GM_GreenMaterial.cs / CommandExecutor.ViewVisibility.cs,
    /// with the green-building shared-parameter-schema logic (67-field
    /// GreenMaterial_* Mat1-6 slots, RFA family injection/backup) intentionally
    /// NOT ported — that is bespoke to REVIT_MCP_study's business workflow and
    /// requires a shared-parameter file this project does not ship.
    /// </summary>
    public static class MaterialHelpers
    {
        public static bool MaterialExistsByName(Document doc, string name)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TypeNameExists(Document doc, ElementType source, string newTypeName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(source.GetType())
                .Cast<ElementType>()
                .Any(t => t.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Find or create a pure standalone Material with a Graphics-tab color.</summary>
        public static Material GetOrCreatePureMaterial(Document doc, string name, Color color)
        {
            Material mat = new FilteredElementCollector(doc)
                .OfClass(typeof(Material)).Cast<Material>()
                .FirstOrDefault(m => m.Name == name);

            if (mat == null)
            {
                ElementId id = Material.Create(doc, name);
                mat = doc.GetElement(id) as Material;
            }

            mat.Color = color;
            mat.Transparency = 0;
            return mat;
        }

        public static CompoundStructure GetCompoundStructureForElement(Element typeElem)
        {
            if (typeElem is WallType wt) return wt.GetCompoundStructure();
            if (typeElem is FloorType ft) return ft.GetCompoundStructure();
            if (typeElem is CeilingType ct) return ct.GetCompoundStructure();
            return null;
        }

        public static void SetCompoundStructureForElement(Element typeElem, CompoundStructure cs)
        {
            if (typeElem is WallType wt) { wt.SetCompoundStructure(cs); return; }
            if (typeElem is FloorType ft) { ft.SetCompoundStructure(cs); return; }
            if (typeElem is CeilingType ct) { ct.SetCompoundStructure(cs); return; }
        }

        public static List<string> GetCompoundStructureMaterials(Document doc, ElementType type)
        {
            var materials = new List<string>();
            CompoundStructure cs = GetCompoundStructureForElement(type);

            if (cs == null)
            {
                Parameter matParam = type.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                if (matParam != null && matParam.HasValue)
                {
                    Material mat = doc.GetElement(matParam.AsElementId()) as Material;
                    if (mat != null) materials.Add(mat.Name);
                }
                return materials;
            }

            for (int i = 0; i < cs.LayerCount; i++)
            {
                ElementId matId = cs.GetMaterialId(i);
                if (matId != ElementId.InvalidElementId)
                {
                    Material mat = doc.GetElement(matId) as Material;
                    materials.Add(mat?.Name ?? $"(ID:{matId.GetIntValue()})");
                }
                else
                {
                    materials.Add("<By Category>");
                }
            }
            return materials;
        }

        /// <summary>
        /// Visible-face "slots" for batch material assignment: Wall gets exterior
        /// (Layer 0) + interior (last layer), Floor gets top (Layer 0), Ceiling
        /// gets bottom (last layer), Mullion/FamilySymbol use a parameter instead
        /// of a CompoundStructure layer (LayerIndex -1).
        /// </summary>
        public static List<(int LayerIndex, string SlotKind)> GetMaterialSlots(Element typeElem)
        {
            var slots = new List<(int, string)>();

            if (typeElem is WallType wt)
            {
                CompoundStructure cs = wt.GetCompoundStructure();
                if (cs != null && cs.LayerCount > 0)
                {
                    slots.Add((0, "Wall.Exterior(Layer0)"));
                    if (cs.LayerCount > 1) slots.Add((cs.LayerCount - 1, "Wall.Interior(LayerLast)"));
                }
                else
                {
                    slots.Add((-1, "Wall.ParamFallback"));
                }
            }
            else if (typeElem is FloorType ft)
            {
                CompoundStructure cs = ft.GetCompoundStructure();
                slots.Add(cs != null && cs.LayerCount > 0 ? (0, "Floor.Top(Layer0)") : (-1, "Floor.ParamFallback"));
            }
            else if (typeElem is CeilingType ct)
            {
                CompoundStructure cs = ct.GetCompoundStructure();
                slots.Add(cs != null && cs.LayerCount > 0 ? (cs.LayerCount - 1, "Ceiling.Bottom(LayerLast)") : (-1, "Ceiling.ParamFallback"));
            }
            else if (typeElem is MullionType)
            {
                slots.Add((-1, "Mullion.Material"));
            }
            else if (typeElem is FamilySymbol)
            {
                slots.Add((-1, "StructuralMaterial"));
            }
            return slots;
        }

        public static ElementId GetSlotMaterialId(Element typeElem, int layerIndex)
        {
            if (layerIndex >= 0)
            {
                CompoundStructure cs = GetCompoundStructureForElement(typeElem);
                return cs != null && layerIndex < cs.LayerCount ? cs.GetMaterialId(layerIndex) : ElementId.InvalidElementId;
            }

            if (typeElem is FamilySymbol fs)
            {
                Parameter mp = fs.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (mp == null || !mp.HasValue) mp = fs.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                return mp?.AsElementId() ?? ElementId.InvalidElementId;
            }
            if (typeElem is ElementType et)
            {
                Parameter mp = et.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                return mp?.AsElementId() ?? ElementId.InvalidElementId;
            }
            return ElementId.InvalidElementId;
        }

        public static void SetSlotMaterialId(Element typeElem, int layerIndex, ElementId newMatId)
        {
            if (layerIndex >= 0)
            {
                CompoundStructure cs = GetCompoundStructureForElement(typeElem);
                if (cs == null || layerIndex >= cs.LayerCount)
                    throw new Exception($"Layer {layerIndex} is out of range for this CompoundStructure");
                cs.SetMaterialId(layerIndex, newMatId);
                SetCompoundStructureForElement(typeElem, cs);
                return;
            }

            if (typeElem is FamilySymbol fs)
            {
                Parameter mp = fs.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (mp == null || mp.IsReadOnly) mp = fs.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                if (mp != null && !mp.IsReadOnly) { mp.Set(newMatId); return; }
                throw new Exception("FamilySymbol material parameter is read-only or missing");
            }
            if (typeElem is MullionType)
            {
                Parameter mp = ((ElementType)typeElem).get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                if (mp != null && !mp.IsReadOnly) { mp.Set(newMatId); return; }
                throw new Exception("Mullion material parameter is read-only or missing");
            }
            if (typeElem is ElementType et)
            {
                Parameter mp = et.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                if (mp != null && !mp.IsReadOnly) { mp.Set(newMatId); return; }
                throw new Exception("ElementType material parameter is read-only or missing");
            }
            throw new Exception($"Unsupported type element: {typeElem.GetType().Name}");
        }

        public static void SetCompoundStructureAllLayers(ElementType type, ElementId newMatId)
        {
            CompoundStructure cs = GetCompoundStructureForElement(type);
            if (cs == null)
            {
                Parameter matParam = type.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                if (matParam != null && !matParam.IsReadOnly) matParam.Set(newMatId);
                return;
            }
            for (int i = 0; i < cs.LayerCount; i++) cs.SetMaterialId(i, newMatId);
            SetCompoundStructureForElement(type, cs);
        }

        public static Material FindOrCreateDuplicateMaterial(Document doc, Material original, string suffix)
        {
            string newName = $"{original.Name}_{suffix}";
            Material existing = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .FirstOrDefault(m => m.Name == newName);
            return existing ?? original.Duplicate(newName);
        }

        public static string GenerateUniqueAssetName(Document doc, string baseName)
        {
            var existing = new HashSet<string>(
                new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement))
                    .Cast<AppearanceAssetElement>().Select(a => a.Name));
            if (!existing.Contains(baseName)) return baseName;
            int n = 1;
            while (existing.Contains($"{baseName}_{n}")) n++;
            return $"{baseName}_{n}";
        }

        /// <summary>
        /// Ensure a Material has its own AppearanceAssetElement (duplicating a
        /// generic one, or one shared with another Material), then set its
        /// diffuse color and optional roughness. Opens its own Transaction —
        /// must be called OUTSIDE any enclosing Transaction, since
        /// AppearanceAssetEditScope.Commit(true) needs its own top-level one.
        /// </summary>
        public static void UpdateAppearanceAsset(Document doc, Material mat, Color color, double? roughness = null)
        {
            using (Transaction t = new Transaction(doc, "Update Appearance Asset"))
            {
                t.Start();
                ElementId assetId = mat.AppearanceAssetId;

                if (assetId == ElementId.InvalidElementId)
                {
                    var sourceAsset = new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement))
                        .Cast<AppearanceAssetElement>().FirstOrDefault();
                    if (sourceAsset != null)
                    {
                        string newName = GenerateUniqueAssetName(doc, mat.Name + "_Appearance");
                        AppearanceAssetElement newAsset = sourceAsset.Duplicate(newName);
                        mat.AppearanceAssetId = newAsset.Id;
                        assetId = newAsset.Id;
                    }
                }
                else
                {
                    bool shared = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                        .Any(m => m.Id != mat.Id && m.AppearanceAssetId == assetId);
                    if (shared)
                    {
                        AppearanceAssetElement original = doc.GetElement(assetId) as AppearanceAssetElement;
                        if (original != null)
                        {
                            string newName = GenerateUniqueAssetName(doc, mat.Name + "_Appearance");
                            AppearanceAssetElement duplicated = original.Duplicate(newName);
                            mat.AppearanceAssetId = duplicated.Id;
                            assetId = duplicated.Id;
                        }
                    }
                }

                if (assetId == ElementId.InvalidElementId) { t.RollBack(); return; }

                using (AppearanceAssetEditScope editScope = new AppearanceAssetEditScope(doc))
                {
                    Asset editableAsset = editScope.Start(assetId);
                    SetAssetDiffuseColor(editableAsset, color);
                    if (roughness.HasValue) SetAssetRoughness(editableAsset, roughness.Value);
                    editScope.Commit(true);
                }
                t.Commit();
            }
        }

        private static readonly string[] DiffuseColorCandidates =
        {
            "generic_diffuse", "common_Tint_color", "ceramic_color", "plastic_color",
            "wood_color", "concrete_color_by_object", "masonrycmu_color", "stone_color_by_object", "metal_f0",
        };

        public static void SetAssetDiffuseColor(Asset asset, Color color)
        {
            foreach (string propName in DiffuseColorCandidates)
                TrySetColorProperty(asset.FindByName(propName), color);

            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty prop = asset[i];
                string n = prop.Name?.ToLowerInvariant() ?? "";
                if (n.Contains("color") || n.Contains("diffuse") || n.Contains("tint"))
                    TrySetColorProperty(prop, color);
            }
        }

        private static void TrySetColorProperty(AssetProperty prop, Color color)
        {
            if (prop is AssetPropertyDoubleArray4d colorProp)
            {
                try { colorProp.SetValueAsColor(color); } catch { /* read-only or connected */ }
            }
        }

        public static void SetAssetRoughness(Asset asset, double roughness)
        {
            roughness = Math.Max(0, Math.Min(1, roughness));
            double glossiness = 1.0 - roughness;
            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty prop = asset[i];
                string n = prop.Name?.ToLowerInvariant() ?? "";
                if (n.Contains("roughness")) TrySetDoubleProperty(prop, roughness);
                else if (n.Contains("glossiness") || n.Contains("shininess")) TrySetDoubleProperty(prop, glossiness);
            }
        }

        private static void TrySetDoubleProperty(AssetProperty prop, double value)
        {
            try
            {
                if (prop is AssetPropertyDouble d) d.Value = value;
                else if (prop is AssetPropertyFloat f) f.Value = (float)value;
            }
            catch { /* read-only or connected */ }
        }

        public static MaterialFunctionAssignment ParseMaterialFunctionAssignment(string s)
        {
            switch (s?.Trim())
            {
                case "Structure": return MaterialFunctionAssignment.Structure;
                case "Substrate": return MaterialFunctionAssignment.Substrate;
                case "Insulation": return MaterialFunctionAssignment.Insulation;
                case "Finish1": return MaterialFunctionAssignment.Finish1;
                case "Finish2": return MaterialFunctionAssignment.Finish2;
                case "Membrane": return MaterialFunctionAssignment.Membrane;
                default:
                    throw new Exception($"Unsupported layerFunction: '{s}' (supported: Structure/Substrate/Insulation/Finish1/Finish2/Membrane)");
            }
        }

        public static bool IsShellFunction(MaterialFunctionAssignment function)
        {
            return function == MaterialFunctionAssignment.Finish1
                || function == MaterialFunctionAssignment.Finish2
                || function == MaterialFunctionAssignment.Membrane;
        }

        public static FillPatternElement GetOrCreateGridModelPattern(Document doc, string name, double spacingMm)
        {
            FillPatternElement existing = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>().FirstOrDefault(x => x.Name == name);
            if (existing != null) return existing;

            double spacingFeet = spacingMm / 304.8;
            FillPattern fp = new FillPattern(name, FillPatternTarget.Model, FillPatternHostOrientation.ToHost);
            var grids = new List<FillGrid>
            {
                new FillGrid { Angle = 0, Origin = new UV(0, 0), Offset = spacingFeet, Shift = 0 },
                new FillGrid { Angle = Math.PI / 2, Origin = new UV(0, 0), Offset = spacingFeet, Shift = 0 },
            };
            fp.SetFillGrids(grids);
            return FillPatternElement.Create(doc, fp);
        }

        public static FillPatternElement GetOrCreateWoodModelPattern(Document doc, string name, double spacingMm)
        {
            FillPatternElement existing = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>().FirstOrDefault(x => x.Name == name);
            if (existing != null) return existing;

            double spacingFeet = spacingMm / 304.8;
            FillPattern fp = new FillPattern(name, FillPatternTarget.Model, FillPatternHostOrientation.ToHost);
            var grids = new List<FillGrid>
            {
                new FillGrid { Angle = 0, Origin = new UV(0, 0), Offset = spacingFeet, Shift = spacingFeet / 3.0 },
            };
            fp.SetFillGrids(grids);
            return FillPatternElement.Create(doc, fp);
        }

        public static void CollectFamilySymbolTypes(Document doc, BuiltInCategory[] categories, List<object> typeInfos)
        {
            var allInstances = new List<Element>();
            foreach (var cat in categories)
                allInstances.AddRange(new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsNotElementType().ToList());

            var symbols = new HashSet<ElementId>();
            foreach (var cat in categories)
            {
                foreach (FamilySymbol fs in new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsElementType().Cast<FamilySymbol>())
                {
                    if (symbols.Contains(fs.Id)) continue;
                    symbols.Add(fs.Id);

                    int instanceCount = allInstances.Count(e => e.GetTypeId() == fs.Id);
                    string structuralMat = "<none>";
                    Parameter matParam = fs.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                    if (matParam == null || matParam.IsReadOnly) matParam = fs.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                    if (matParam != null && matParam.HasValue)
                    {
                        Material mat = doc.GetElement(matParam.AsElementId()) as Material;
                        if (mat != null) structuralMat = mat.Name;
                    }

                    typeInfos.Add(new
                    {
                        TypeId = fs.Id.GetIntValue(),
                        TypeName = fs.Name,
                        FamilyName = fs.FamilyName,
                        InstanceCount = instanceCount,
                        Materials = new List<string> { structuralMat },
                    });
                }
            }
        }

        /// <summary>Robustly parse an element-id array from a JArray, a JSON-array string, or a comma-separated string.</summary>
        public static List<int> ParseIdArray(Newtonsoft.Json.Linq.JToken token)
        {
            var result = new List<int>();
            if (token == null) return result;

            if (token is Newtonsoft.Json.Linq.JArray arr)
            {
                result.AddRange(arr.Select(id => (int)id));
                return result;
            }

            string str = token.ToString().Trim();
            if (str.StartsWith("["))
            {
                try
                {
                    var parsed = Newtonsoft.Json.Linq.JArray.Parse(str);
                    result.AddRange(parsed.Select(id => (int)id));
                    return result;
                }
                catch { /* fall through to comma-split */ }
            }

            foreach (var part in str.Split(','))
                if (int.TryParse(part.Trim(), out int id)) result.Add(id);
            return result;
        }
    }
}
