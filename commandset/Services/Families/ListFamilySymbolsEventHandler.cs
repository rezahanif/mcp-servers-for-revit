using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Families
{
    public class ListFamilySymbolsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private JObject _parameters;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(JObject parameters)
        {
            _parameters = parameters;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                string filterName = _parameters?["filter"]?.Value<string>();

                var symbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(s => string.IsNullOrEmpty(filterName) ||
                               (s.FamilyName != null && s.FamilyName.IndexOf(filterName, StringComparison.OrdinalIgnoreCase) >= 0) ||
                               (s.Name != null && s.Name.IndexOf(filterName, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Select(s => new
                    {
                        Id = s.Id.GetIntValue(),
                        Name = s.Name,
                        FamilyName = s.FamilyName ?? "<NULL>",
                        Category = s.Category?.Name ?? "<NO_CAT>"
                    })
                    .Take(100)
                    .ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully listed {symbols.Count} family symbols",
                    Response = new
                    {
                        Symbols = symbols
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error listing family symbols: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "ListFamilySymbolsEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
