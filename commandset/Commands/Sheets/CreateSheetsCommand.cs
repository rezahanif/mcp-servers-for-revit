using System.Linq;
using System.Collections.Generic;
using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Sheets;
using RevitMCPCommandSet.Services.Sheets;

namespace RevitMCPCommandSet.Commands.Sheets
{
    /// <summary>
    /// Command to batch-create blank sheets
    /// </summary>
    public class CreateSheetsCommand : ExternalEventCommandBase
    {
        private CreateSheetsEventHandler _handler => (CreateSheetsEventHandler)Handler;

        public override string CommandName => "create_sheets";

        public CreateSheetsCommand(UIApplication uiApp)
            : base(new CreateSheetsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                int titleBlockId = parameters["titleBlockId"]?.Value<int>() ?? 0;
                if (titleBlockId == 0)
                    throw new ArgumentNullException(nameof(titleBlockId), "No title block ID provided");

                List<SheetSpec> sheets = parameters["sheets"]?.ToObject<List<SheetSpec>>();
                if (sheets == null || sheets.Count == 0)
                    throw new ArgumentNullException(nameof(sheets), "No sheets provided");

                _handler.SetParameters(titleBlockId, sheets);

                if (RaiseAndWaitForCompletion(20000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Create sheets operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create sheets: {ex.Message}");
            }
        }
    }
}
