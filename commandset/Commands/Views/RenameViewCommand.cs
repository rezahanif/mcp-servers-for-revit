using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Views;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Views
{
    /// <summary>
    /// Command to rename a view in Revit.
    /// </summary>
    public class RenameViewCommand : ExternalEventCommandBase
    {
        private RenameViewEventHandler _handler => (RenameViewEventHandler)Handler;

        public override string CommandName => "rename_view";

        public RenameViewCommand(UIApplication uiApp)
            : base(new RenameViewEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long viewId = parameters["viewId"]?.ToObject<long>() ?? 0;
                string newName = parameters["newName"]?.ToObject<string>();

                if (viewId <= 0)
                    throw new ArgumentException("A valid viewId is required");
                if (string.IsNullOrWhiteSpace(newName))
                    throw new ArgumentException("A non-empty newName is required");

                _handler.SetParameters(viewId, newName);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Rename view operation timed out");
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
    }
}
