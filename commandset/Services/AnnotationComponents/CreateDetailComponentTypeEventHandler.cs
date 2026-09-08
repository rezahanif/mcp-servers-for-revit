using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.AnnotationComponents;

/// <summary>
///     Event handler to duplicate a detail item family type, naming it
///     "&lt;sheetNumber&gt;-&lt;sheetName&gt;-&lt;detailName&gt;" and filling its
///     sheet/detail-number type parameters. Ported from REVIT_MCP_study's
///     CommandExecutor.DetailComponent.cs (CreateDetailComponentType).
/// </summary>
public class CreateDetailComponentTypeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private UIApplication _uiApp;
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    private string _sheetNumber, _detailName, _familyName, _detailNumber;
    public AIResult<object> Result { get; private set; }

    public void SetParameters(string sheetNumber, string detailName, string familyName, string detailNumber)
    {
        _sheetNumber = sheetNumber;
        _detailName = detailName;
        _familyName = familyName;
        _detailNumber = detailNumber;
        _resetEvent.Reset();
    }

    public void Execute(UIApplication uiapp)
    {
        _uiApp = uiapp;
        Document doc = _uiApp.ActiveUIDocument.Document;

        try
        {
            var sheet = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .FirstOrDefault(s => s.SheetNumber == _sheetNumber);

            if (sheet == null)
            {
                Result = new AIResult<object> { Success = false, Message = $"Sheet number not found: {_sheetNumber}" };
                return;
            }

            string sheetName = sheet.Name;

            FamilySymbol baseSymbol;
            if (!string.IsNullOrEmpty(_familyName))
            {
                baseSymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.FamilyName != null &&
                        (s.FamilyName.Equals(_familyName, StringComparison.OrdinalIgnoreCase) || s.FamilyName.Contains(_familyName)));
            }
            else
            {
                baseSymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.FamilyName != null && s.FamilyName.Contains("AE-圖號"));
            }

            if (baseSymbol == null)
            {
                Result = new AIResult<object> { Success = false, Message = $"Base detail item family not found: {_familyName ?? "AE-圖號"}" };
                return;
            }

            string targetTypeName = $"{_sheetNumber}-{sheetName}-{_detailName}";

            var existingSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.FamilyName == baseSymbol.FamilyName && s.Name == targetTypeName);

            if (existingSymbol != null)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Type already exists: {targetTypeName}",
                    Response = new { ExistingTypeId = existingSymbol.Id.GetIntValue() }
                };
                return;
            }

            using (Transaction t = new Transaction(doc, "Create Detail Component Type"))
            {
                t.Start();

                var newSymbol = baseSymbol.Duplicate(targetTypeName) as FamilySymbol;
                if (newSymbol == null)
                {
                    t.RollBack();
                    Result = new AIResult<object> { Success = false, Message = $"Failed to duplicate type: {targetTypeName}" };
                    return;
                }

                SetIfExists(newSymbol, "詳圖圖號", _sheetNumber);
                SetIfExists(newSymbol, "圖說名稱", sheetName ?? "");
                SetIfExists(newSymbol, "詳圖名稱", _detailName ?? "");
                SetIfExists(newSymbol, "詳圖編號", _detailNumber);

                t.Commit();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Created detail component type '{targetTypeName}'.",
                    Response = new
                    {
                        TypeId = newSymbol.Id.GetIntValue(),
                        TypeName = newSymbol.Name,
                        SheetNumber = _sheetNumber,
                        SheetName = sheetName,
                        DetailName = _detailName,
                        DetailNumber = _detailNumber
                    }
                };
            }
        }
        catch (Exception ex)
        {
            Result = new AIResult<object> { Success = false, Message = $"Error creating detail component type: {ex.Message}" };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    private static void SetIfExists(FamilySymbol symbol, string paramName, string value)
    {
        Parameter p = symbol.LookupParameter(paramName);
        if (p != null && !p.IsReadOnly) p.Set(value);
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public string GetName() => "Create Detail Component Type";
}
