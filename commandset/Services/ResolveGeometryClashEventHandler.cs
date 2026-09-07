using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class ResolveGeometryClashEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public long ElementIdA { get; set; }
        public long ElementIdB { get; set; }
        public string Action { get; set; } = "cut"; // "cut", "uncut", "join", "unjoin", "switch_join_order"

        public AIResult<string> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Resolve Geometry Clash";

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<string>
                    {
                        Success = false,
                        Message = "No active Revit document.",
                        Response = null
                    };
                    return;
                }

#if REVIT2024_OR_GREATER
                var idA = new ElementId(ElementIdA);
                var idB = new ElementId(ElementIdB);
#else
                var idA = new ElementId((int)ElementIdA);
                var idB = new ElementId((int)ElementIdB);
#endif
                var elemA = doc.GetElement(idA);
                var elemB = doc.GetElement(idB);

                if (elemA == null || elemB == null)
                {
                    Result = new AIResult<string>
                    {
                        Success = false,
                        Message = $"Could not resolve element IDs: {ElementIdA} or {ElementIdB}.",
                        Response = null
                    };
                    return;
                }

                string actionName = Action.ToLowerInvariant().Trim();
                TransactionUtils.ExecuteInTransaction(doc, $"Resolve Clash: {actionName}", () =>
                {
                    switch (actionName)
                    {
                        case "cut":
                            SolidSolidCutUtils.AddCutBetweenSolids(doc, elemA, elemB);
                            break;
                        case "uncut":
                            SolidSolidCutUtils.RemoveCutBetweenSolids(doc, elemA, elemB);
                            break;
                        case "join":
                            if (!JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB))
                            {
                                JoinGeometryUtils.JoinGeometry(doc, elemA, elemB);
                            }
                            break;
                        case "unjoin":
                            if (JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB))
                            {
                                JoinGeometryUtils.UnjoinGeometry(doc, elemA, elemB);
                            }
                            break;
                        case "switch_join_order":
                            JoinGeometryUtils.SwitchJoinOrder(doc, elemA, elemB);
                            break;
                        default:
                            throw new ArgumentException($"Unsupported action '{Action}'. Choose from 'cut', 'uncut', 'join', 'unjoin', 'switch_join_order'.");
                    }
                });

                Result = new AIResult<string>
                {
                    Success = true,
                    Message = $"Successfully applied '{actionName}' geometry operation between element {ElementIdA} and {ElementIdB}.",
                    Response = actionName
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<string>
                {
                    Success = false,
                    Message = $"Failed to resolve geometry clash: {ex.Message}",
                    Response = null
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }
    }
}
