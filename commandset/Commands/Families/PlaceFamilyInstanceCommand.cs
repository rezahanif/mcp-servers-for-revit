using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Commands.Families
{
    public class PlaceFamilyInstanceCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private PlaceFamilyInstanceEventHandler _handler => (PlaceFamilyInstanceEventHandler)Handler;

        public override string CommandName => "place_family_instance";

        public PlaceFamilyInstanceCommand(UIApplication uiApp)
            : base(new PlaceFamilyInstanceEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    var data = parameters?["data"] as JArray;
                    if (data == null || data.Count == 0)
                    {
                        throw new ArgumentException("data must be a non-empty array of family instance placements");
                    }

                    var items = new List<PlaceFamilyInstanceEventHandler.PlacementData>();
                    foreach (var token in data)
                    {
                        var loc = token["location"];
                        if (loc == null) throw new ArgumentException("location {x, y, z} is required for each instance");

                        items.Add(new PlaceFamilyInstanceEventHandler.PlacementData
                        {
                            FamilyName = token["familyName"]?.ToString(),
                            TypeName = token["typeName"]?.ToString() ?? throw new ArgumentException("typeName is required"),
                            X = loc["x"]?.Value<double>() ?? 0,
                            Y = loc["y"]?.Value<double>() ?? 0,
                            Z = loc["z"]?.Value<double>() ?? 0,
                            LevelId = token["levelId"]?.Value<long>(),
                            HostId = token["hostId"]?.Value<long>(),
                            Rotation = token["rotation"]?.Value<double>(),
                            FlipFacing = token["flipFacing"]?.Value<bool>() ?? false,
                            FlipHand = token["flipHand"]?.Value<bool>() ?? false
                        });
                    }

                    _handler.Items = items;

                    if (RaiseAndWaitForCompletion(25000))
                    {
                        return _handler.Result;
                    }
                    throw new TimeoutException("Timeout waiting for place_family_instance external event handler");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to place family instance: {ex.Message}", ex);
                }
            }
        }
    }
}
