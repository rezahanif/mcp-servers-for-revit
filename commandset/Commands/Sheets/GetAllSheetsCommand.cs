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
    /// Command to list all sheets in the project
    /// </summary>
    public class GetAllSheetsCommand : ExternalEventCommandBase
    {
        private GetAllSheetsEventHandler _handler => (GetAllSheetsEventHandler)Handler;

        public override string CommandName => "get_all_sheets";

        public GetAllSheetsCommand(UIApplication uiApp)
            : base(new GetAllSheetsEventHandler(), uiApp)
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
                    throw new TimeoutException("Get all sheets operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get sheets: {ex.Message}");
            }
        }
    }
}
