using Autodesk.Revit.DB;
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
    public class GetFamilyTypesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string FamilyName { get; set; }
        public string CategoryName { get; set; }
        public bool IncludeParameters { get; set; }

        public class FamilyTypeInfo
        {
            public long TypeId { get; set; }
            public string TypeName { get; set; }
            public string FamilyName { get; set; }
            public string Category { get; set; }
            public Dictionary<string, string> Parameters { get; set; }
        }

        public AIResult<List<FamilyTypeInfo>> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Get Family Types";

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<List<FamilyTypeInfo>> { Success = false, Message = "No active Revit document.", Response = new List<FamilyTypeInfo>() };
                    return;
                }

                var collector = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>();

                if (!string.IsNullOrEmpty(FamilyName))
                {
                    collector = collector.Where(fs => fs.FamilyName.Equals(FamilyName, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(CategoryName))
                {
                    collector = collector.Where(fs => fs.Category != null && fs.Category.Name.Equals(CategoryName, StringComparison.OrdinalIgnoreCase));
                }

                var list = new List<FamilyTypeInfo>();
                foreach (var symbol in collector)
                {
                    var info = new FamilyTypeInfo
                    {
                        TypeId = symbol.Id.GetValue(),
                        TypeName = symbol.Name,
                        FamilyName = symbol.FamilyName,
                        Category = symbol.Category?.Name ?? "Unknown"
                    };

                    if (IncludeParameters)
                    {
                        info.Parameters = new Dictionary<string, string>();
                        foreach (Parameter p in symbol.Parameters)
                        {
                            if (p.HasValue && !string.IsNullOrEmpty(p.Definition.Name))
                            {
                                info.Parameters[p.Definition.Name] = p.AsValueString() ?? p.AsString() ?? "";
                            }
                        }
                    }

                    list.Add(info);
                }

                Result = new AIResult<List<FamilyTypeInfo>>
                {
                    Success = true,
                    Message = $"Found {list.Count} family type(s).",
                    Response = list
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<FamilyTypeInfo>>
                {
                    Success = false,
                    Message = $"Error retrieving family types: {ex.Message}",
                    Response = new List<FamilyTypeInfo>()
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
