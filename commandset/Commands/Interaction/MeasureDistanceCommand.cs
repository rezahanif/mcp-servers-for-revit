using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Interaction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Interaction
{
    /// <summary>
    /// Command to measure 3D distance between two points (in mm).
    /// </summary>
    public class MeasureDistanceCommand : ExternalEventCommandBase
    {
        private MeasureDistanceEventHandler _handler => (MeasureDistanceEventHandler)Handler;

        public override string CommandName => "measure_distance";

        public MeasureDistanceCommand(UIApplication uiApp)
            : base(new MeasureDistanceEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                double p1x = parameters["point1X"]?.ToObject<double>() ?? 0;
                double p1y = parameters["point1Y"]?.ToObject<double>() ?? 0;
                double p1z = parameters["point1Z"]?.ToObject<double>() ?? 0;
                double p2x = parameters["point2X"]?.ToObject<double>() ?? 0;
                double p2y = parameters["point2Y"]?.ToObject<double>() ?? 0;
                double p2z = parameters["point2Z"]?.ToObject<double>() ?? 0;

                _handler.SetParameters(p1x, p1y, p1z, p2x, p2y, p2z);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Measure distance operation timed out");
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
    }
}
