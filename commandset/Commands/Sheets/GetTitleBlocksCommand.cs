using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.Sheets;

namespace RevitMCPCommandSet.Commands.Sheets
{
    /// <summary>
    /// Command to list all title block types in the project
    /// </summary>
    public class GetTitleBlocksCommand : ExternalEventCommandBase
    {
        private GetTitleBlocksEventHandler _handler => (GetTitleBlocksEventHandler)Handler;

        public override string CommandName => "get_titleblocks";

        public GetTitleBlocksCommand(UIApplication uiApp)
            : base(new GetTitleBlocksEventHandler(), uiApp)
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
                    throw new TimeoutException("Get title blocks operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get title blocks: {ex.Message}");
            }
        }
    }
}
