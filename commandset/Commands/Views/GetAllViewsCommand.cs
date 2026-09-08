using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Views;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Views
{
    /// <summary>
    /// Command to get all non-template, printable views in the project.
    /// </summary>
    public class GetAllViewsCommand : ExternalEventCommandBase
    {
        private GetAllViewsEventHandler _handler => (GetAllViewsEventHandler)Handler;

        public override string CommandName => "get_all_views";

        public GetAllViewsCommand(UIApplication uiApp)
            : base(new GetAllViewsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string viewType = parameters["viewType"]?.ToObject<string>();
                string levelName = parameters["levelName"]?.ToObject<string>();

                _handler.SetParameters(viewType, levelName);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get all views operation timed out");
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
