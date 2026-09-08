using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.AnnotationComponents;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.AnnotationComponents
{
    public class CreateCorridorDimensionCommand : ExternalEventCommandBase
    {
        private CreateCorridorDimensionEventHandler _handler => (CreateCorridorDimensionEventHandler)Handler;

        public override string CommandName => "create_corridor_dimension";

        public CreateCorridorDimensionCommand(UIApplication uiApp)
            : base(new CreateCorridorDimensionEventHandler(), uiApp)
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
                    throw new TimeoutException("Create corridor dimension operation timed out");
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
