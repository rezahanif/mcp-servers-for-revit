using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Interaction
{
    public class MeasureDistanceEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        private double _p1x, _p1y, _p1z;
        private double _p2x, _p2y, _p2z;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(double p1x, double p1y, double p1z, double p2x, double p2y, double p2z)
        {
            _p1x = p1x; _p1y = p1y; _p1z = p1z;
            _p2x = p2x; _p2y = p2y; _p2z = p2z;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                // Convert mm to feet
                XYZ point1 = new XYZ(_p1x / 304.8, _p1y / 304.8, _p1z / 304.8);
                XYZ point2 = new XYZ(_p2x / 304.8, _p2y / 304.8, _p2z / 304.8);

                double distanceFeet = point1.DistanceTo(point2);
                double distanceMm = distanceFeet * 304.8;

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Measured distance: {Math.Round(distanceMm, 2)} mm",
                    Response = new
                    {
                        Distance = Math.Round(distanceMm, 2),
                        Unit = "mm",
                        Point1 = new { X = _p1x, Y = _p1y, Z = _p1z },
                        Point2 = new { X = _p2x, Y = _p2y, Z = _p2z }
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error measuring distance: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "MeasureDistanceEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
