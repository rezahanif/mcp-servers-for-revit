using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Common
{
    public class DocumentWarningInfo
    {
        public string Description { get; set; }
        public string Severity { get; set; }
        public List<long> ElementIds { get; set; } = new List<long>();
        public List<string> ElementSummaries { get; set; } = new List<string>();
    }
}
