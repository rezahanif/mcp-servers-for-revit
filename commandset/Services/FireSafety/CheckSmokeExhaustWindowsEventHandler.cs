using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.FireSafety;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.FireSafety
{
    public class CheckSmokeExhaustWindowsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private CheckSmokeExhaustWindowsRequest _request;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(CheckSmokeExhaustWindowsRequest request)
        {
            _request = request;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;
                string levelName = _request?.LevelName;
                string ceilingHeightSource = _request?.CeilingHeightSource ?? "room_parameter";
                bool colorize = _request?.Colorize ?? true;
                double smokeZoneHeight = _request?.SmokeZoneHeight > 0 ? _request.SmokeZoneHeight : 800;

                string[] defaultExcludeKeywords = { "走廊", "corridor", "hall", "樓梯", "stair", "電梯", "elevator", "lift", "管道", "shaft", "機房", "mechanical", "廁所", "toilet", "restroom", "浴室", "bath", "玄關", "vestibule", "lobby", "陽台", "balcony" };
                string[] excludeKeywords = _request?.ExcludeKeywords != null && _request.ExcludeKeywords.Count > 0
                    ? _request.ExcludeKeywords.ToArray()
                    : defaultExcludeKeywords;

                const double FEET_TO_MM = 304.8;
                const double SQ_FEET_TO_SQ_M = 0.092903;

                Level level = FireSafetyHelpers.FindLevel(doc, levelName, false);
                double levelElevation = level.Elevation;

                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.LevelId == level.Id && r.Area > 0)
                    .ToList();

                var roomResults = new List<object>();
                int totalRooms = 0;
                int roomsChecked = 0;
                int roomsCompliant = 0;
                int roomsFailed = 0;
                int roomsSkipped = 0;
                int roomsSkippedNonResidential = 0;

                SpatialElementBoundaryOptions boundaryOptions = new SpatialElementBoundaryOptions();

                foreach (Room room in rooms)
                {
                    totalRooms++;
                    double roomAreaSqM = room.Area * SQ_FEET_TO_SQ_M;

                    string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                    string roomNameLower = roomName.ToLower();
                    if (excludeKeywords.Any(kw => roomNameLower.Contains(kw.ToLower())))
                    {
                        roomsSkippedNonResidential++;
                        continue;
                    }

                    if (roomAreaSqM <= 50)
                    {
                        roomsSkipped++;
                        continue;
                    }

                    roomsChecked++;

                    double ceilingHeight = GetRoomCeilingHeight(doc, room, ceilingHeightSource);
                    double smokeZoneTop = ceilingHeight;
                    double smokeZoneBottom = ceilingHeight - smokeZoneHeight;

                    var windowResults = new List<object>();
                    var processedWindowIds = new HashSet<int>();
                    double sumEffectiveArea = 0;

                    IList<IList<BoundarySegment>> segments = room.GetBoundarySegments(boundaryOptions);
                    if (segments != null)
                    {
                        foreach (IList<BoundarySegment> segmentList in segments)
                        {
                            foreach (BoundarySegment segment in segmentList)
                            {
                                Element element = doc.GetElement(segment.ElementId);
                                if (element is Wall wall)
                                {
                                    IList<ElementId> insertIds = wall.FindInserts(true, false, false, false);
                                    foreach (ElementId insertId in insertIds)
                                    {
                                        int insertInt = insertId.GetIntValue();
                                        if (processedWindowIds.Contains(insertInt)) continue;

                                        Element insert = doc.GetElement(insertId);
                                        if (insert is FamilyInstance fi &&
                                            fi.Category != null &&
                                            fi.Category.Id.GetIntValue() == (int)BuiltInCategory.OST_Windows)
                                        {
                                            processedWindowIds.Add(insertInt);

                                            BuiltInParameter[] widthBips = { BuiltInParameter.FAMILY_WIDTH_PARAM, BuiltInParameter.WINDOW_WIDTH };
                                            string[] widthNames = { "粗略寬度", "寬度", "Width", "寬" };
                                            BuiltInParameter[] heightBips = { BuiltInParameter.FAMILY_HEIGHT_PARAM, BuiltInParameter.WINDOW_HEIGHT };
                                            string[] heightNames = { "粗略高度", "高度", "Height", "高" };
                                            BuiltInParameter[] sillBips = { BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM };
                                            string[] sillNames = { "窗台高度", "Sill Height", "底高度" };
                                            BuiltInParameter[] headBips = { BuiltInParameter.INSTANCE_HEAD_HEIGHT_PARAM };
                                            string[] headNames = { "窗頂高度", "Head Height", "頂高度" };

                                            Element symbol = fi.Symbol;
                                            double? wValRaw = FireSafetyHelpers.GetParamValue(fi, widthBips, widthNames) ?? FireSafetyHelpers.GetParamValue(symbol, widthBips, widthNames);
                                            double width = (wValRaw ?? 0) * FEET_TO_MM;

                                            double? hValRaw = FireSafetyHelpers.GetParamValue(fi, heightBips, heightNames) ?? FireSafetyHelpers.GetParamValue(symbol, heightBips, heightNames);
                                            double height = (hValRaw ?? 0) * FEET_TO_MM;

                                            double sillHeightRaw = FireSafetyHelpers.GetParamValue(fi, sillBips, sillNames) ?? 0;
                                            double sillHeight = sillHeightRaw * FEET_TO_MM;

                                            double headHeightRaw = FireSafetyHelpers.GetParamValue(fi, headBips, headNames) ?? (sillHeightRaw + (hValRaw ?? 0));
                                            double headHeight = headHeightRaw * FEET_TO_MM;

                                            double headHeightCapped = Math.Min(headHeight, smokeZoneTop);
                                            bool isInSmokeZone = headHeightCapped > smokeZoneBottom;

                                            double heightInZone = 0;
                                            double areaInZone = 0;
                                            if (isInSmokeZone)
                                            {
                                                double effectiveBottom = Math.Max(sillHeight, smokeZoneBottom);
                                                heightInZone = Math.Max(0, headHeightCapped - effectiveBottom);
                                                areaInZone = (width / 1000.0) * (heightInZone / 1000.0);
                                            }

                                            var (operationType, openingRatio, needsConfirm, note) =
                                                FireSafetyHelpers.GetWindowOperationType(fi.Symbol.FamilyName, fi.Symbol.Name);

                                            double effectiveArea = areaInZone * openingRatio;
                                            sumEffectiveArea += effectiveArea;

                                            windowResults.Add(new
                                            {
                                                WindowId = insertInt,
                                                FamilyName = fi.Symbol.FamilyName,
                                                TypeName = fi.Symbol.Name,
                                                Width = Math.Round(width, 1),
                                                Height = Math.Round(height, 1),
                                                HeadHeight = Math.Round(headHeight, 1),
                                                IsInSmokeZone = isInSmokeZone,
                                                EffectiveArea = Math.Round(effectiveArea, 4),
                                                OperationType = operationType,
                                                Note = note
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    double totalEffectiveArea = sumEffectiveArea;
                    double requiredArea = roomAreaSqM * 0.02;
                    bool isCompliant = totalEffectiveArea >= requiredArea;
                    if (isCompliant) roomsCompliant++;
                    else roomsFailed++;

                    roomResults.Add(new
                    {
                        RoomId = room.Id.GetIntValue(),
                        RoomName = roomName,
                        RoomAreaSqM = Math.Round(roomAreaSqM, 2),
                        CeilingHeightMm = Math.Round(ceilingHeight, 1),
                        RequiredAreaSqM = Math.Round(requiredArea, 4),
                        EffectiveAreaSqM = Math.Round(totalEffectiveArea, 4),
                        IsCompliant = isCompliant,
                        Windows = windowResults
                    });
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Smoke exhaust window check complete: {roomsChecked} checked, {roomsCompliant} compliant, {roomsFailed} non-compliant",
                    Response = new
                    {
                        LevelName = level.Name,
                        TotalRooms = totalRooms,
                        RoomsChecked = roomsChecked,
                        RoomsCompliant = roomsCompliant,
                        RoomsFailed = roomsFailed,
                        RoomsSkippedSmallArea = roomsSkipped,
                        RoomsSkippedNonResidential = roomsSkippedNonResidential,
                        Results = roomResults
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error checking smoke exhaust windows: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private double GetRoomCeilingHeight(Document doc, Room room, string source)
        {
            const double FEET_TO_MM = 304.8;
            Parameter upperLimitParam = room.get_Parameter(BuiltInParameter.ROOM_UPPER_LEVEL);
            Parameter upperOffsetParam = room.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET);

            if (upperLimitParam != null && upperOffsetParam != null)
            {
                ElementId upperLevelId = upperLimitParam.AsElementId();
                double upperOffset = upperOffsetParam.AsDouble();
                Level roomLevel = doc.GetElement(room.LevelId) as Level;
                double roomElevation = roomLevel?.Elevation ?? 0;

                if (upperLevelId != ElementId.InvalidElementId)
                {
                    Level upperLevel = doc.GetElement(upperLevelId) as Level;
                    if (upperLevel != null)
                        return (upperLevel.Elevation - roomElevation + upperOffset) * FEET_TO_MM;
                }
                return upperOffset * FEET_TO_MM;
            }

            BoundingBoxXYZ bb = room.get_BoundingBox(null);
            if (bb != null) return (bb.Max.Z - bb.Min.Z) * FEET_TO_MM;
            return 3000.0;
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "CheckSmokeExhaustWindowsEventHandler";
    }
}
