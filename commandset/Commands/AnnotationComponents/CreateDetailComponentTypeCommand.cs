using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.AnnotationComponents;

namespace RevitMCPCommandSet.Commands.AnnotationComponents;

/// <summary>
///     Command to duplicate a detail item family type and auto-fill sheet/detail parameters
/// </summary>
public class CreateDetailComponentTypeCommand : ExternalEventCommandBase
{
    private CreateDetailComponentTypeEventHandler _handler => (CreateDetailComponentTypeEventHandler)Handler;

    public CreateDetailComponentTypeCommand(UIApplication uiApp)
        : base(new CreateDetailComponentTypeEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_detail_component_type";

    public override object Execute(JObject parameters, string requestId)
    {
        string sheetNumber = parameters?["sheetNumber"]?.Value<string>();
        string detailName = parameters?["detailName"]?.Value<string>();
        string familyName = parameters?["familyName"]?.Value<string>();
        string detailNumber = parameters?["detailNumber"]?.Value<string>() ?? "1";

        if (string.IsNullOrEmpty(sheetNumber))
            throw new ArgumentNullException(nameof(sheetNumber), "sheetNumber is required");
        if (string.IsNullOrEmpty(detailName))
            throw new ArgumentNullException(nameof(detailName), "detailName is required");

        _handler.SetParameters(sheetNumber, detailName, familyName, detailNumber);

        if (RaiseAndWaitForCompletion(15000))
        {
            return _handler.Result;
        }
        throw new TimeoutException("Create detail component type operation timed out");
    }
}
