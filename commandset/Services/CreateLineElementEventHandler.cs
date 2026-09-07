using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class CreateLineElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;
        /// <summary>
        /// 事件等待对象
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        /// <summary>
        /// 创建数据（传入数据）
        /// </summary>
        public List<LineElement> CreatedInfo { get; private set; }
        /// <summary>
        /// 执行结果（传出数据）
        /// </summary>
        public AIResult<List<int>> Result { get; private set; }
        /// <summary>
        /// Something happened that the caller would not otherwise see, but the
        /// element WAS created — a defaulted type, an inexact level match, an
        /// ignored thickness, an overlap with existing geometry.
        /// </summary>
        private List<string> _warnings = new List<string>();

        /// <summary>
        /// An entry that produced NO element. Separate from _warnings because the
        /// two demand different reactions and used to be indistinguishable: both
        /// arrived as prose under one Success=true, so a caller could not tell
        /// "built, but check it" from "not built at all".
        /// </summary>
        private List<string> _failures = new List<string>();

        /// <summary>
        /// Existing walls per level, built lazily once per Execute and discarded
        /// after — a snapshot taken before this batch adds anything, which is
        /// exactly what "was something already here?" should be asked against.
        /// </summary>
        private Dictionary<ElementId, List<Wall>> _wallsByLevel;

        public string _wallName = "常规 - ";
        public string _ductName = "矩形风管 - ";

        /// <summary>
        /// 设置创建的参数
        /// </summary>
        public void SetParameters(List<LineElement> data)
        {
            CreatedInfo = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementIds = new List<int>();
                _warnings.Clear();
                _failures.Clear();
                _wallsByLevel = null; // rebuilt lazily against the CURRENT document
                foreach (var data in CreatedInfo)
                {
                    int requestedTypeId = data.TypeId;

                    // Step0 获取构件类型
                    BuiltInCategory builtInCategory = BuiltInCategory.INVALID;
                    Enum.TryParse(data.Category.Replace(".", ""), true, out builtInCategory);

                    // Step1 获取标高和偏移
                    // levelId (exact) wins over baseLevel (nearest-elevation guess);
                    // a guess that did not land where the caller asked leaves a warning
                    // instead of silently hosting on a neighbouring storey.
                    Level baseLevel = null;
                    Level topLevel = null;
                    double topOffset = -1;  // ft
                    double baseOffset = -1; // ft
                    baseLevel = doc.ResolveLevel(data.LevelId, data.BaseLevel, out string levelWarning);
                    if (levelWarning != null)
                        _warnings.Add(levelWarning);
                    // Null-check BEFORE dereferencing: the old order read
                    // baseLevel.Elevation first, so a document with no Levels threw a
                    // NullReferenceException here and the whole batch was reported as a
                    // generic failure rather than as the one specific, fixable cause.
                    if (baseLevel == null)
                        continue;
                    baseOffset = (data.BaseOffset + data.BaseLevel) / 304.8 - baseLevel.Elevation;
                    topLevel = doc.FindNearestLevel((data.BaseLevel + data.BaseOffset + data.Height) / 304.8);
                    topOffset = (data.BaseLevel + data.BaseOffset + data.Height) / 304.8 - (topLevel?.Elevation ?? 0);

                    // Step2 获取族类型
                    FamilySymbol symbol = null;
                    WallType wallType = null;
                    DuctType ductType = null;

                    if (data.TypeId != -1 && data.TypeId != 0)
                    {
                        ElementId typeELeId = new ElementId(data.TypeId);
                        if (typeELeId != null)
                        {
                            Element typeEle = doc.GetElement(typeELeId);
                            if (typeEle != null && typeEle is FamilySymbol)
                            {
                                symbol = typeEle as FamilySymbol;
                                // 获取symbol的Category对象并转换为BuiltInCategory枚举
                                builtInCategory = (BuiltInCategory)symbol.Category.Id.GetIntValue();
                            }
                            else if (typeEle != null && typeEle is WallType)
                            {
                                wallType = typeEle as WallType;
                                builtInCategory = (BuiltInCategory)wallType.Category.Id.GetIntValue();
                            }
                            else if (typeEle != null && typeEle is DuctType)
                            {
                                ductType = typeEle as DuctType;
                                builtInCategory = (BuiltInCategory)ductType.Category.Id.GetIntValue();
                            }
                        }
                    }
                    if (builtInCategory == BuiltInCategory.INVALID)
                        continue;
                    switch (builtInCategory)
                    {
                        case BuiltInCategory.OST_Walls:
                            if (wallType == null)
                            {
                                // A typeId the caller ASKED FOR and that did not resolve is
                                // now a failure, not a substitution. Substituting produced
                                // models built from an arbitrary first-in-collector type —
                                // wrong material, wrong assembly — while still reporting
                                // success. Only an ABSENT typeId still falls back.
                                if (requestedTypeId != -1 && requestedTypeId != 0)
                                {
                                    _failures.Add($"Requested wall typeId {requestedTypeId} is not a WallType in this document. " +
                                                  $"Nothing was created for this entry. Resolve a real id with get_available_family_types.");
                                    continue;
                                }
                                wallType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(WallType))
                                    .Cast<WallType>()
                                    .FirstOrDefault();
                                if (wallType == null)
                                {
                                    _failures.Add($"No wall types available in project.");
                                    continue;
                                }
                                _warnings.Add($"No typeId given for a wall. Defaulted to '{wallType.Name}' (ID: {wallType.Id.GetValue()}), " +
                                              $"which is whichever type came first and is probably not the one you want.");
                            }
                            // `thickness` reaches Wall.Create nowhere — width is the
                            // WallType's compound structure. Silently ignoring it let a
                            // caller believe it had set a width it had not.
                            if (data.Thickness > 0)
                            {
                                double actualMm = wallType.Width * 304.8;
                                if (Math.Abs(actualMm - data.Thickness) > 1.0)
                                {
                                    _warnings.Add($"thickness {data.Thickness}mm was ignored: wall width comes from type " +
                                                  $"'{wallType.Name}', which is {actualMm:0.##}mm. Pass a different typeId to change width.");
                                }
                            }
                            WarnOnOverlappingWall(data, wallType, baseLevel);
                            break;
                        case BuiltInCategory.OST_DuctCurves:
                            if (ductType == null)
                            {
                                // Requested typeId was invalid or not provided, fall back to first available rectangular duct
                                if (requestedTypeId != -1 && requestedTypeId != 0)
                                {
                                    _failures.Add($"Requested duct typeId {requestedTypeId} is not a DuctType in this document. " +
                                                  $"Nothing was created for this entry.");
                                    continue;
                                }
                                ductType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(DuctType))
                                    .Cast<DuctType>()
                                    .FirstOrDefault(d => d.Shape == ConnectorProfileType.Rectangular);
                                if (ductType == null)
                                {
                                    _failures.Add($"No rectangular duct types available in project.");
                                    continue;
                                }
                                _warnings.Add($"No typeId given for a duct. Defaulted to '{ductType.Name}' (ID: {ductType.Id.GetValue()}).");
                            }
                            break;
                        default:
                            if (symbol == null)
                            {
                                if (requestedTypeId != -1 && requestedTypeId != 0)
                                {
                                    _failures.Add($"Requested typeId {requestedTypeId} is not a FamilySymbol in this document. " +
                                                  $"Nothing was created for this entry. Resolve a real id with get_available_family_types.");
                                    continue;
                                }
                                symbol = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilySymbol))
                                    .OfCategory(builtInCategory)
                                    .Cast<FamilySymbol>()
                                    .FirstOrDefault(fs => fs.IsActive); // 获取激活的类型作为默认类型
                                if (symbol == null)
                                {
                                    symbol = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilySymbol))
                                    .OfCategory(builtInCategory)
                                    .Cast<FamilySymbol>()
                                    .FirstOrDefault();
                                }
                                if (symbol == null)
                                {
                                    _failures.Add($"No family types available for category {builtInCategory}.");
                                    continue;
                                }
                                _warnings.Add($"No typeId given for {builtInCategory}. Defaulted to " +
                                              $"'{symbol.FamilyName}: {symbol.Name}' (ID: {symbol.Id.GetValue()}), " +
                                              $"which is whichever type came first and is probably not the one you want.");
                            }
                            break;
                    }

                    // Step3 调用通用方法创建族实例
                    using (Transaction transaction = new Transaction(doc, "创建点状构件"))
                    {
                        transaction.Start();
                        switch (builtInCategory)
                        {
                            case BuiltInCategory.OST_Walls:
                                Wall wall = null;
                                wall = Wall.Create
                                (
                                  doc,
                                  JZLine.ToLine(data.LocationLine),
                                  wallType.Id,
                                  baseLevel.Id,
                                  data.Height / 304.8,
                                  baseOffset,
                                  false,
                                  false
                                );
                                if (wall != null)
                                {
                                    elementIds.Add(wall.Id.GetIntValue());
                                }
                                break;
                            case BuiltInCategory.OST_DuctCurves:
                                Duct duct = null;
                                // 获取MEP系统类型（必需）
                                MEPSystemType mepSystemType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(MEPSystemType))
                                    .Cast<MEPSystemType>()
                                    .FirstOrDefault(m => m.SystemClassification == MEPSystemClassification.SupplyAir);

                                if (mepSystemType != null)
                                {
                                    duct = Duct.Create(
                                        doc,
                                        mepSystemType.Id,
                                        ductType.Id,
                                        baseLevel.Id,
                                        JZLine.ToLine(data.LocationLine).GetEndPoint(0),
                                        JZLine.ToLine(data.LocationLine).GetEndPoint(1)
                                    );

                                    if (duct != null)
                                    {
                                        // 设置高度偏移
                                        Parameter offsetParam = duct.get_Parameter(BuiltInParameter.RBS_OFFSET_PARAM);
                                        if (offsetParam != null)
                                            offsetParam.Set(baseOffset);
                                        elementIds.Add(duct.Id.GetIntValue());
                                    }
                                }
                                break;
                            default:
                                if (!symbol.IsActive)
                                    symbol.Activate();

                                // 调用FamilyInstance通用创建方法
                                var instance = doc.CreateInstance(symbol, null, JZLine.ToLine(data.LocationLine), baseLevel, topLevel, baseOffset, topOffset);
                                if (instance != null)
                                {
                                    elementIds.Add(instance.Id.GetIntValue());
                                }
                                break;
                        }
                        //doc.Refresh();
                        transaction.Commit();
                    }
                }
                // Success now means "every entry produced an element". A batch where
                // some entries failed reports false, so a caller that checks only
                // this flag cannot mistake a partial build for a complete one.
                string message = $"Created {elementIds.Count} of {CreatedInfo.Count} element(s).";
                if (_failures.Count > 0)
                {
                    message += "\n\n✖ Failed:\n  • " + string.Join("\n  • ", _failures);
                }
                if (_warnings.Count > 0)
                {
                    message += "\n\n⚠ Warnings:\n  • " + string.Join("\n  • ", _warnings);
                }
                Result = new AIResult<List<int>>
                {
                    Success = _failures.Count == 0,
                    Message = message,
                    Warnings = new List<string>(_warnings),
                    Failures = new List<string>(_failures),
                    Response = elementIds,
                };
            }
            catch (Exception ex)
            {
                // No TaskDialog: this runs inside a Revit external event with no
                // human at the keyboard, so a modal blocks the handler until one
                // arrives. The caller then times out at 120s having been told
                // nothing — which is exactly how a committed transaction came back
                // looking like a failure and got retried into duplicates.
                Result = new AIResult<List<int>>
                {
                    Success = false,
                    Message = $"Error creating line-based element(s): {ex.Message}",
                    Failures = new List<string> { ex.ToString() },
                };
            }
            finally
            {
                _resetEvent.Set(); // 通知等待线程操作已完成
            }
        }

        /// <summary>
        /// 等待创建完成
        /// </summary>
        /// <param name="timeoutMilliseconds">超时时间（毫秒）</param>
        /// <returns>操作是否在超时前完成</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName 实现
        /// </summary>
        public string GetName()
        {
            return "创建线状构件";
        }

        /// <summary>
        /// Warn when the requested wall would land on top of one that is already
        /// there. Nothing in this command ever looked at the existing model, so a
        /// caller re-running a step — or recovering from a timeout that had in fact
        /// committed — stacked a second wall in the same place with no signal at
        /// all. This does NOT block the create: only the caller knows whether a
        /// second leaf is intended.
        /// </summary>
        private void WarnOnOverlappingWall(LineElement data, WallType wallType, Level level)
        {
            try
            {
                Line requested = JZLine.ToLine(data.LocationLine);
                XYZ a0 = requested.GetEndPoint(0), a1 = requested.GetEndPoint(1);

                // Collected ONCE per Execute, not once per entry. A collector scan
                // inside the batch loop is O(entries x walls), and a large batch
                // against a large model is exactly the case where the caller can
                // least afford the extra seconds — a duplicate-detection aid that
                // caused timeouts would be worse than no aid at all.
                //
                // Same storey only: walls on different levels sharing a plan
                // position are the normal case in a multi-storey model.
                if (_wallsByLevel == null)
                {
                    _wallsByLevel = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .Cast<Wall>()
                        .Where(w => w.Location is LocationCurve)
                        .GroupBy(w => w.LevelId)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }
                if (!_wallsByLevel.TryGetValue(level.Id, out var existing))
                    return;

                foreach (Wall w in existing)
                {
                    if (w.Location is not LocationCurve lc || lc.Curve is not Line other)
                        continue;

                    XYZ b0 = other.GetEndPoint(0), b1 = other.GetEndPoint(1);

                    // Collinear-and-overlapping in plan: both endpoints of the
                    // requested line sit on the existing line's infinite extension,
                    // and the two spans share more than a touching point.
                    XYZ dir = (b1 - b0);
                    if (dir.GetLength() < 1e-9)
                        continue;
                    dir = dir.Normalize();

                    if (PlanDistanceToLine(a0, b0, dir) > CoincidenceToleranceFt ||
                        PlanDistanceToLine(a1, b0, dir) > CoincidenceToleranceFt)
                        continue;

                    double ta0 = (a0 - b0).DotProduct(dir), ta1 = (a1 - b0).DotProduct(dir);
                    double lo = Math.Min(ta0, ta1), hi = Math.Max(ta0, ta1);
                    double overlap = Math.Min(hi, other.Length) - Math.Max(lo, 0);
                    if (overlap <= CoincidenceToleranceFt)
                        continue;

                    _warnings.Add($"A wall already runs along this line on Level '{level.Name}': " +
                                  $"'{w.Name}' (ID: {w.Id.GetValue()}), overlapping by {overlap * 304.8:0}mm. " +
                                  $"Creating anyway — delete the existing one first if this was meant to replace it.");
                    return;
                }
            }
            catch
            {
                // A geometry probe must never cost the caller the create it asked
                // for; an unreported overlap is strictly better than a lost wall.
            }
        }

        /// <summary>1mm in feet — Revit's own coincidence threshold for this kind of check.</summary>
        private const double CoincidenceToleranceFt = 1.0 / 304.8;

        /// <summary>Perpendicular distance from <paramref name="p"/> to the line (origin, dir), ignoring Z.</summary>
        private static double PlanDistanceToLine(XYZ p, XYZ origin, XYZ dir)
        {
            XYZ v = new XYZ(p.X - origin.X, p.Y - origin.Y, 0);
            XYZ d = new XYZ(dir.X, dir.Y, 0);
            if (d.GetLength() < 1e-9)
                return v.GetLength();
            d = d.Normalize();
            return (v - d.Multiply(v.DotProduct(d))).GetLength();
        }

        /// <summary>
        /// 创建或获取指定厚度的墙体类型
        /// </summary>
        /// <param name="doc">Revit文档</param>
        /// <param name="width">宽度（ft）</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private WallType CreateOrGetWallType(Document doc, double width = 200 / 304.8)
        {
            // 如果没有有效的类型
            // 先查找是否存在指定厚度的建筑墙类型
            WallType existingType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(WallType))
                                    .Cast<WallType>()
                                    .FirstOrDefault(w => w.Name == $"{_wallName}{width * 304.8}mm");
            if (existingType != null)
                return existingType;

            // 不存在则创建新的墙体类型，基于基本墙
            WallType baseWallType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(WallType))
                                    .Cast<WallType>()
                                    .FirstOrDefault(w => w.Name.Contains("常规")); ;
            if (baseWallType == null)
            {
                baseWallType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(WallType))
                                    .Cast<WallType>()
                                    .FirstOrDefault(); ;
            }

            if (baseWallType == null)
                throw new InvalidOperationException("未找到可用的基础墙类型");

            // 复制墙体类型
            WallType newWallType = null;
            newWallType = baseWallType.Duplicate($"{_wallName}{width * 304.8}mm") as WallType;

            // 设置墙厚
            CompoundStructure cs = newWallType.GetCompoundStructure();
            if (cs != null)
            {
                // 获取原始层的材料ID
                ElementId materialId = cs.GetLayers().First().MaterialId;

                // 创建新的单层结构
                CompoundStructureLayer newLayer = new CompoundStructureLayer(
                    width,  // 宽度（转换为英尺）
                    MaterialFunctionAssignment.Structure,  // 功能分配
                    materialId  // 材料ID
                );

                // 创建新的复合结构
                IList<CompoundStructureLayer> newLayers = new List<CompoundStructureLayer> { newLayer };
                cs.SetLayers(newLayers);

                // 应用新的复合结构
                newWallType.SetCompoundStructure(cs);
            }
            return newWallType;
        }

        /// <summary>
        /// 创建或获取指定尺寸的风管类型
        /// </summary>
        /// <param name="doc">Revit文档</param>
        /// <param name="width">宽度（ft）</param>
        /// <param name="height">高度（ft）</param>
        /// <returns>风管类型</returns>
        private DuctType CreateOrGetDuctType(Document doc, double width, double height)
        {
            string typeName = $"{_ductName}{width * 304.8}x{height * 304.8}mm";

            // 先查找是否存在指定尺寸的风管类型
            DuctType existingType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(DuctType))
                                    .Cast<DuctType>()
                                    .FirstOrDefault(d => d.Name == typeName && d.Shape == ConnectorProfileType.Rectangular);

            if (existingType != null)
                return existingType;

            // 不存在则创建新的风管类型，基于已有的矩形风管类型
            DuctType baseDuctType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(DuctType))
                                    .Cast<DuctType>()
                                    .FirstOrDefault(d => d.Shape == ConnectorProfileType.Rectangular);

            if (baseDuctType == null)
                throw new InvalidOperationException("未找到可用的基础矩形风管类型");

            // 复制风管类型
            DuctType newDuctType = baseDuctType.Duplicate(typeName) as DuctType;

            // 设置风管尺寸参数
            Parameter widthParam = newDuctType.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
            Parameter heightParam = newDuctType.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

            if (widthParam != null && heightParam != null)
            {
                widthParam.Set(width);
                heightParam.Set(height);
            }

            return newDuctType;
        }

    }
}
