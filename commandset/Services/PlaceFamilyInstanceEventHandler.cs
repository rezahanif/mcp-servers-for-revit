using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class PlaceFamilyInstanceEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public class PlacementData
        {
            public string FamilyName { get; set; }
            public string TypeName { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
            public long? LevelId { get; set; }
            public long? HostId { get; set; }
            public double? Rotation { get; set; }
            public bool FlipFacing { get; set; }
            public bool FlipHand { get; set; }
        }

        public List<PlacementData> Items { get; set; } = new List<PlacementData>();
        public AIResult<List<long>> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 25000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Place Family Instance";

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<List<long>> { Success = false, Message = "No active Revit document.", Response = new List<long>() };
                    return;
                }

                var createdIds = new List<long>();
                var warnings = new List<string>();
                var failures = new List<string>();

                using var transaction = new Transaction(doc, "Place Family Instances");
                var preprocessor = TransactionUtils.ConfigureWarningSuppression(transaction);
                transaction.Start();

                foreach (var item in Items)
                {
                    // Find symbol
                    var symbol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(fs =>
                            (string.IsNullOrEmpty(item.FamilyName) || fs.FamilyName.Equals(item.FamilyName, StringComparison.OrdinalIgnoreCase)) &&
                            fs.Name.Equals(item.TypeName, StringComparison.OrdinalIgnoreCase));

                    if (symbol == null)
                    {
                        failures.Add($"Could not find family symbol '{item.FamilyName} : {item.TypeName}'.");
                        continue;
                    }

                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        doc.Regenerate();
                    }

                    XYZ pt = new XYZ(item.X / 304.8, item.Y / 304.8, item.Z / 304.8);
                    FamilyInstance instance = null;

                    Level level = null;
                    if (item.LevelId.HasValue)
                    {
#if REVIT2024_OR_GREATER
                        level = doc.GetElement(new ElementId(item.LevelId.Value)) as Level;
#else
                        level = doc.GetElement(new ElementId((int)item.LevelId.Value)) as Level;
#endif
                    }

                    Element host = null;
                    if (item.HostId.HasValue)
                    {
#if REVIT2024_OR_GREATER
                        host = doc.GetElement(new ElementId(item.HostId.Value));
#else
                        host = doc.GetElement(new ElementId((int)item.HostId.Value));
#endif
                    }

                    if (host != null && level != null)
                    {
                        instance = doc.Create.NewFamilyInstance(pt, symbol, host, level, StructuralType.NonStructural);
                    }
                    else if (host != null)
                    {
                        instance = doc.Create.NewFamilyInstance(pt, symbol, host, StructuralType.NonStructural);
                    }
                    else if (level != null)
                    {
                        instance = doc.Create.NewFamilyInstance(pt, symbol, level, StructuralType.NonStructural);
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(pt, symbol, StructuralType.NonStructural);
                    }

                    if (instance != null)
                    {
                        if (item.Rotation.HasValue && Math.Abs(item.Rotation.Value) > 1e-4)
                        {
                            Line axis = Line.CreateBound(pt, pt + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(doc, instance.Id, axis, item.Rotation.Value * Math.PI / 180.0);
                        }

                        if (item.FlipFacing && instance.CanFlipFacing)
                        {
                            instance.flipFacing();
                        }

                        if (item.FlipHand && instance.CanFlipHand)
                        {
                            instance.flipHand();
                        }

                        createdIds.Add(instance.Id.GetValue());
                    }
                }

                transaction.Commit();

                if (preprocessor.Warnings.Count > 0) warnings.AddRange(preprocessor.Warnings);
                if (preprocessor.Failures.Count > 0) failures.AddRange(preprocessor.Failures);

                Result = new AIResult<List<long>>
                {
                    Success = failures.Count == 0 && createdIds.Count > 0,
                    Message = $"Placed {createdIds.Count} of {Items.Count} family instance(s).",
                    Response = createdIds,
                    Warnings = warnings,
                    Failures = failures
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<long>>
                {
                    Success = false,
                    Message = $"Error placing family instances: {ex.Message}",
                    Response = new List<long>()
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
