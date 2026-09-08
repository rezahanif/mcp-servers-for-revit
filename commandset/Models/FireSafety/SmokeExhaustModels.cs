using System.Collections.Generic;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.FireSafety
{
    public class CheckSmokeExhaustWindowsRequest
    {
        [JsonProperty("levelName")] public string LevelName { get; set; }
        [JsonProperty("ceilingHeightSource")] public string CeilingHeightSource { get; set; } = "room_parameter";
        [JsonProperty("colorize")] public bool Colorize { get; set; } = true;
        [JsonProperty("smokeZoneHeight")] public double SmokeZoneHeight { get; set; } = 800;
        [JsonProperty("excludeKeywords")] public List<string> ExcludeKeywords { get; set; }
    }

    public class CheckFloorEffectiveOpeningsRequest
    {
        [JsonProperty("levelName")] public string LevelName { get; set; }
        [JsonProperty("colorize")] public bool Colorize { get; set; } = true;
    }

    public class ExportSmokeReviewExcelRequest
    {
        [JsonProperty("levelName")] public string LevelName { get; set; }
        [JsonProperty("ceilingHeightSource")] public string CeilingHeightSource { get; set; } = "room_parameter";
        [JsonProperty("outputPath")] public string OutputPath { get; set; }
    }
}
