using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Commands.Civil
{
    /// <summary>
    /// create_direct_shape — generate custom 3D solid geometry (civil retaining walls, bridge piers, culverts, terrain blocks).
    /// </summary>
    public class CreateDirectShapeCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private CreateDirectShapeEventHandler _handler => (CreateDirectShapeEventHandler)Handler;

        public override string CommandName => "create_direct_shape";

        public CreateDirectShapeCommand(UIApplication uiApp)
            : base(new CreateDirectShapeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    _handler.Name = parameters?["name"]?.ToString() ?? "DirectShape_Element";
                    _handler.Category = parameters?["category"]?.ToString() ?? "OST_GenericModel";
                    _handler.ShapeType = parameters?["shapeType"]?.ToString() ?? "box";

                    var origin = parameters?["origin"];
                    _handler.X = origin?["x"]?.Value<double>() ?? 0;
                    _handler.Y = origin?["y"]?.Value<double>() ?? 0;
                    _handler.Z = origin?["z"]?.Value<double>() ?? 0;

                    _handler.Length = parameters?["length"]?.Value<double>() ?? 2000;
                    _handler.Width = parameters?["width"]?.Value<double>() ?? 1000;
                    _handler.Height = parameters?["height"]?.Value<double>() ?? 1000;

                    var ptsArray = parameters?["polygonPoints"] as JArray;
                    if (ptsArray != null && ptsArray.Count > 0)
                    {
                        var pts = new List<Tuple<double, double>>();
                        foreach (var ptToken in ptsArray)
                        {
                            double px = ptToken["x"]?.Value<double>() ?? 0;
                            double py = ptToken["y"]?.Value<double>() ?? 0;
                            pts.Add(new Tuple<double, double>(px, py));
                        }
                        _handler.PolygonPoints = pts;
                    }
                    else
                    {
                        _handler.PolygonPoints = null;
                    }

                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for create_direct_shape external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to create DirectShape: {ex.Message}", ex);
                }
            }
        }
    }
}
