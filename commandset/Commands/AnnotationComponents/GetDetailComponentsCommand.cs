using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.AnnotationComponents;

namespace RevitMCPCommandSet.Commands.AnnotationComponents;

/// <summary>
///     Command to query detail component instances in the project
/// </summary>
public class GetDetailComponentsCommand : ExternalEventCommandBase
{
    private GetDetailComponentsEventHandler _handler => (GetDetailComponentsEventHandler)Handler;

    public GetDetailComponentsCommand(UIApplication uiApp)
        : base(new GetDetailComponentsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_detail_components";

    public override object Execute(JObject parameters, string requestId)
    {
        string familyName = parameters?["familyName"]?.Value<string>() ?? "";

        _handler.SetParameters(familyName);

        if (RaiseAndWaitForCompletion(15000))
        {
            return _handler.Result;
        }
        throw new TimeoutException("Get detail components operation timed out");
    }
}
