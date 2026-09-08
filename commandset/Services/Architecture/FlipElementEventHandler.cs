using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    public class FlipElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private int _elementId;
        private string _flipType;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(int elementId, string flipType)
        {
            _elementId = elementId;
            _flipType = flipType ?? "facing";
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;
                Element element = doc.GetElement(new ElementId(_elementId));
                if (element == null)
                    throw new Exception($"Element ID {_elementId} not found");

                if (!(element is FamilyInstance familyInstance))
                    throw new Exception($"Element ID {_elementId} is not a FamilyInstance and cannot be flipped");

                using (Transaction trans = new Transaction(doc, $"Flip Element {_elementId}"))
                {
                    trans.Start();

                    string ft = _flipType.Trim().ToLower();
                    if (ft == "facing")
                    {
                        if (familyInstance.CanFlipFacing)
                            familyInstance.flipFacing();
                        else
                            throw new Exception("Element does not support flipping facing direction");
                    }
                    else if (ft == "hand")
                    {
                        if (familyInstance.CanFlipHand)
                            familyInstance.flipHand();
                        else
                            throw new Exception("Element does not support flipping hand direction");
                    }
                    else
                    {
                        throw new Exception("Invalid flipType; use 'facing' or 'hand'");
                    }

                    trans.Commit();

                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"Successfully flipped element {_elementId} ({_flipType})",
                        Response = new
                        {
                            ElementId = _elementId,
                            FlipType = _flipType,
                            Message = "Successfully flipped element"
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error flipping element: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "FlipElementEventHandler";
    }
}
