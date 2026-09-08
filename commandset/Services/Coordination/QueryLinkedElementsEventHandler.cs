using System;
using System.Threading;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Coordination
{
    public class QueryLinkedElementsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication _uiApp;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private JObject _parameters;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(JObject parameters)
        {
            _parameters = parameters;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            _uiApp = uiapp;

            try
            {
                var helper = new LinkedModelHelper(_uiApp);
                var data = helper.QueryLinkedElements(_parameters);

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = "Successfully queried linked elements",
                    Response = data
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error querying linked elements: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "QueryLinkedElementsEventHandler";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}
