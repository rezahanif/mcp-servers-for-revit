using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Architecture;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Architecture
{
    public class GetRoomInfoCommand : ExternalEventCommandBase
    {
        private GetRoomInfoEventHandler _handler => (GetRoomInfoEventHandler)Handler;

        public override string CommandName => "get_room_info";

        public GetRoomInfoCommand(UIApplication uiApp)
            : base(new GetRoomInfoEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long? roomId = parameters["roomId"]?.ToObject<long?>();
                string roomName = parameters["roomName"]?.ToObject<string>();

                _handler.SetParameters(roomId, roomName);

                if (RaiseAndWaitForCompletion(15000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Get room info operation timed out");
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
