using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System;
using System.IO;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class LoadFamilyEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string FilePath { get; set; }
        public bool Overwrite { get; set; }

        public AIResult<object> Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName() => "Load Family";

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new AIResult<object> { Success = false, Message = "No active Revit document." };
                    return;
                }

                if (!File.Exists(FilePath))
                {
                    Result = new AIResult<object> { Success = false, Message = $"File not found: {FilePath}" };
                    return;
                }

                Family loadedFamily = null;
                bool success = false;

                TransactionUtils.ExecuteInTransaction(doc, "Load Family", () =>
                {
                    if (Overwrite)
                    {
                        success = doc.LoadFamily(FilePath, new FamilyLoadOptions(), out loadedFamily);
                    }
                    else
                    {
                        success = doc.LoadFamily(FilePath, out loadedFamily);
                    }
                });

                if (success && loadedFamily != null)
                {
                    Result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"Successfully loaded family '{loadedFamily.Name}' (ID: {loadedFamily.Id.GetValue()}).",
                        Response = new
                        {
                            FamilyId = loadedFamily.Id.GetValue(),
                            FamilyName = loadedFamily.Name
                        }
                    };
                }
                else
                {
                    Result = new AIResult<object>
                    {
                        Success = false,
                        Message = $"Family could not be loaded or is already loaded without overwrite enabled."
                    };
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"Error loading family: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private class FamilyLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = true;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = true;
                return true;
            }
        }
    }
}
