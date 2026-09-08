using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Architecture;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class SetProjectUnitsCommand : ExternalEventCommandBase
    {
        private SetProjectUnitsEventHandler _handler => (SetProjectUnitsEventHandler)Handler;

        public override string CommandName => "set_project_units";

        public SetProjectUnitsCommand(UIApplication uiApp)
            : base(new SetProjectUnitsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                _handler.SetParameters(parameters);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Set project units operation timed out");
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
