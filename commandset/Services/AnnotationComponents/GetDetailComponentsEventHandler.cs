using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.AnnotationComponents;

/// <summary>
///     Event handler for querying detail component instances.
///     Ported from REVIT_MCP_study's CommandExecutor.DetailComponent.cs (GetDetailComponents).
/// </summary>
public class GetDetailComponentsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private UIApplication _uiApp;
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    public string FamilyNameFilter { get; private set; }
    public AIResult<object> Result { get; private set; }

    public void SetParameters(string familyNameFilter)
    {
        FamilyNameFilter = familyNameFilter;
        _resetEvent.Reset();
    }

    public void Execute(UIApplication uiapp)
    {
        _uiApp = uiapp;
        Document doc = _uiApp.ActiveUIDocument.Document;

        try
        {
            var items = new List<object>();
            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType();

            foreach (var e in collector)
            {
                if (e is not FamilyInstance fi) continue;
                if (!string.IsNullOrEmpty(FamilyNameFilter) && !fi.Symbol.FamilyName.Contains(FamilyNameFilter))
                    continue;

                var parameters = new Dictionary<string, string>();
                foreach (Parameter p in fi.Parameters)
                {
                    string val = p.AsValueString() ?? p.AsString() ?? "";
                    if (!string.IsNullOrEmpty(val))
                        parameters[p.Definition.Name] = val;
                }

                items.Add(new
                {
                    ElementId = fi.Id.GetIntValue(),
                    FamilyName = fi.Symbol.FamilyName,
                    TypeName = fi.Symbol.Name,
                    OwnerViewId = fi.OwnerViewId.GetIntValue(),
                    Parameters = parameters
                });
            }

            Result = new AIResult<object>
            {
                Success = true,
                Message = $"Found {items.Count} detail component instance(s).",
                Response = new { Count = items.Count, Items = items }
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<object>
            {
                Success = false,
                Message = $"Error querying detail components: {ex.Message}"
            };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public string GetName() => "Get Detail Components";
}
