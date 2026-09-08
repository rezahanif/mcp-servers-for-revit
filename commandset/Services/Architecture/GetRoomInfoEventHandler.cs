using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    public class GetRoomInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        private long? _roomId;
        private string _roomName;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(long? roomId, string roomName)
        {
            _roomId = roomId;
            _roomName = roomName;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                Room room = null;

                if (_roomId.HasValue && _roomId.Value > 0)
                {
#if REVIT2024_OR_GREATER
                    room = doc.GetElement(new ElementId(_roomId.Value)) as Room;
#else
                    room = doc.GetElement(new ElementId((int)_roomId.Value)) as Room;
#endif
                }
                else if (!string.IsNullOrEmpty(_roomName))
                {
                    room = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .FirstOrDefault(r => (r.Name != null && r.Name.IndexOf(_roomName, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                             (r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString()?.IndexOf(_roomName, StringComparison.OrdinalIgnoreCase) >= 0));
                }

                if (room == null)
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = _roomId.HasValue ? $"Room not found with ID: {_roomId.Value}" : $"Room not found with name matching: {_roomName}"
                    };
                    return;
                }

                LocationPoint locPoint = room.Location as LocationPoint;
                XYZ center = locPoint?.Point ?? XYZ.Zero;
                BoundingBoxXYZ bbox = room.get_BoundingBox(null);
                double areaM2 = room.Area * 0.092903;

                var boundarySegments = GetRoomBoundarySegments(room);

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Retrieved room info for {room.Name ?? room.Number}",
                    Response = new
                    {
                        ElementId = room.Id.GetIntValue(),
                        Name = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? room.Name,
                        Number = room.Number,
                        Level = doc.GetElement(room.LevelId)?.Name,
                        Area = Math.Round(areaM2, 2),
                        CenterX = Math.Round(center.X * 304.8, 2),
                        CenterY = Math.Round(center.Y * 304.8, 2),
                        BoundingBox = bbox != null ? new
                        {
                            MinX = Math.Round(bbox.Min.X * 304.8, 2),
                            MinY = Math.Round(bbox.Min.Y * 304.8, 2),
                            MaxX = Math.Round(bbox.Max.X * 304.8, 2),
                            MaxY = Math.Round(bbox.Max.Y * 304.8, 2)
                        } : null,
                        BoundarySegments = boundarySegments
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error retrieving room info: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private List<object> GetRoomBoundarySegments(Room room)
        {
            var segments = new List<object>();
            var options = new SpatialElementBoundaryOptions();

            try
            {
                var boundarySegments = room.GetBoundarySegments(options);
                if (boundarySegments == null) return segments;

                foreach (var loop in boundarySegments)
                {
                    foreach (BoundarySegment seg in loop)
                    {
                        var curve = seg.GetCurve();
                        var start = curve.GetEndPoint(0);
                        var end = curve.GetEndPoint(1);

                        segments.Add(new
                        {
                            StartX = Math.Round(start.X * 304.8, 2),
                            StartY = Math.Round(start.Y * 304.8, 2),
                            EndX = Math.Round(end.X * 304.8, 2),
                            EndY = Math.Round(end.Y * 304.8, 2),
                            Length = Math.Round(curve.Length * 304.8, 2)
                        });
                    }
                }
            }
            catch
            {
            }

            return segments;
        }

        public string GetName() => "GetRoomInfoEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
