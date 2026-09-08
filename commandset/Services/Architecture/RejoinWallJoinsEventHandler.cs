using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Architecture
{
    public class RejoinWallJoinsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public AIResult<object> Result { get; private set; }

        public void SetParameters()
        {
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;

                if (WallJoinTracker.Count == 0)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = "No joins to restore",
                        Response = new { Success = true, RejoinedCount = 0, Message = "No joins to restore" }
                    };
                    return;
                }

                int rejoinedCount = 0;
                int storedCount = WallJoinTracker.Count;

                using (Transaction trans = new Transaction(doc, "Rejoin Geometry"))
                {
                    trans.Start();

                    foreach (var pair in WallJoinTracker.Pairs)
                    {
                        try
                        {
                            Element elem1 = doc.GetElement(pair.Item1);
                            Element elem2 = doc.GetElement(pair.Item2);

                            if (elem1 != null && elem2 != null)
                            {
                                if (!JoinGeometryUtils.AreElementsJoined(doc, elem1, elem2))
                                {
                                    JoinGeometryUtils.JoinGeometry(doc, elem1, elem2);
                                    rejoinedCount++;
                                }
                            }
                        }
                        catch { }
                    }

                    trans.Commit();
                }

                WallJoinTracker.Clear();

                Result = new AIResult<object>
                {
                    Success = true,
                    Message = $"Successfully restored {rejoinedCount} of {storedCount} join relationship(s)",
                    Response = new
                    {
                        Success = true,
                        RejoinedCount = rejoinedCount,
                        TotalPairs = storedCount,
                        Message = $"Successfully restored {rejoinedCount} of {storedCount} join relationship(s)"
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error restoring joins: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 30000) => _resetEvent.WaitOne(timeoutMilliseconds);
        public string GetName() => "RejoinWallJoinsEventHandler";
    }
}
