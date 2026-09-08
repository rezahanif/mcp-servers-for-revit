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
    /// Command to auto-renumber "-1" suffixed insert sheets back into the main sequence
    /// </summary>
    public class AutoRenumberSheetsCommand : ExternalEventCommandBase
    {
        private AutoRenumberSheetsEventHandler _handler => (AutoRenumberSheetsEventHandler)Handler;

        public override string CommandName => "auto_renumber_sheets";

        public AutoRenumberSheetsCommand(UIApplication uiApp)
            : base(new AutoRenumberSheetsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters();

                if (RaiseAndWaitForCompletion(30000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Auto-renumber sheets operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to auto-renumber sheets: {ex.Message}");
            }
        }
    }
}
