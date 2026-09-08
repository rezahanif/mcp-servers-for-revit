using RevitMCPCommandSet.Services.Architecture;
using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Architecture
{
    /// <summary>
    /// Command to get available line styles (GraphicsStyles)
    /// </summary>
    public class GetLineStylesCommand : ExternalEventCommandBase
    {
        private GetLineStylesEventHandler _handler => (GetLineStylesEventHandler)Handler;

        public override string CommandName => "get_line_styles";

        public GetLineStylesCommand(UIApplication uiApp)
            : base(new GetLineStylesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get line styles operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get line styles: {ex.Message}");
            }
        }
    }
}
