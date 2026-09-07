using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>
    /// Failure preprocessor that intercepts and suppresses interactive Revit UI dialogs/modals
    /// (such as wall overlap warnings and duplicate element warnings) and captures them as structured text
    /// so the AI assistant receives actionable feedback without freezing the bridge.
    /// </summary>
    public class CaptureWarningsPreprocessor : IFailuresPreprocessor
    {
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Failures { get; } = new List<string>();
        public List<ElementId> FailingElementIds { get; } = new List<ElementId>();

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failList = failuresAccessor.GetFailureMessages();
            if (failList == null || failList.Count == 0)
            {
                return FailureProcessingResult.Continue;
            }

            foreach (var failure in failList)
            {
                var severity = failure.GetSeverity();
                string desc = failure.GetDescriptionText();
                var elementIds = failure.GetFailingElementIds();
                if (elementIds != null && elementIds.Count > 0)
                {
                    FailingElementIds.AddRange(elementIds);
                }

                string idString = elementIds != null && elementIds.Count > 0
                    ? $" (Elements: {string.Join(", ", elementIds.Select(id => id.ToString()))})"
                    : "";

                if (severity == FailureSeverity.Warning)
                {
                    Warnings.Add($"{desc}{idString}");
                    failuresAccessor.DeleteWarning(failure); // Suppresses UI warning popup dialog
                }
                else if (severity == FailureSeverity.Error)
                {
                    Failures.Add($"{desc}{idString}");
                    try
                    {
                        failuresAccessor.ResolveFailure(failure);
                    }
                    catch
                    {
                        // Some errors cannot be resolved programmatically; caught in Failures list
                    }
                }
            }

            return FailureProcessingResult.ProceedWithCommit;
        }
    }
}
