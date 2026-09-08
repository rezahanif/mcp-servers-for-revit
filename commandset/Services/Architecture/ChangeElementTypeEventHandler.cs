using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    /// <summary>
    /// Event handler for changing an element's (or a batch of elements') type
    /// </summary>
    public class ChangeElementTypeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private int? _elementId;
        private List<int> _elementIds;
        private int _typeId;

        public AIResult<object> Result { get; private set; }

        public void SetParameters(int? elementId, List<int> elementIds, int typeId)
        {
            _elementId = elementId;
            _elementIds = elementIds;
            _typeId = typeId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                ElementId newTypeId = new ElementId(_typeId);
                Element targetType = doc.GetElement(newTypeId);
                if (targetType == null)
                    throw new System.Exception($"Target type not found: {_typeId}");

                var allIds = new List<int>();
                if (_elementId.HasValue) allIds.Add(_elementId.Value);
                if (_elementIds != null) allIds.AddRange(_elementIds);

                if (allIds.Count == 0)
                    throw new System.Exception("Provide at least one element ID (elementId or elementIds)");

                int successCount = 0;
                var errors = new List<string>();

                using (Transaction trans = new Transaction(doc, "Change Element Type"))
                {
                    trans.Start();

                    foreach (int id in allIds)
                    {
                        Element elem = doc.GetElement(new ElementId(id));
                        if (elem == null) continue;

#if REVIT2023_OR_GREATER
                        if (!elem.CanHaveTypeAssigned())
                        {
                            errors.Add($"Element {id} does not support type changes");
                            continue;
                        }
#endif

                        try
                        {
                            elem.ChangeTypeId(newTypeId);
                            successCount++;
                        }
                        catch (System.Exception ex)
                        {
                            errors.Add($"Element {id}: {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Changed the type of {successCount} element(s)",
                    Response = new { ChangedCount = successCount },
                    Failures = errors.Count > 0 ? errors : null,
                };
            }
            catch (System.Exception ex)
            {
                Result = new AIResult<object> { Success = false, Message = $"Error changing element type: {ex.Message}" };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Change Element Type";
    }
}
