namespace RevitMCPCommandSet.Models.Common
{
    public class ClashResult
    {
        public long ElementIdA { get; set; }
        public string NameA { get; set; }
        public string CategoryA { get; set; }
        public long ElementIdB { get; set; }
        public string NameB { get; set; }
        public string CategoryB { get; set; }
        public string ClashType { get; set; }
        public string Recommendation { get; set; }
    }
}
