using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Sheets;

/// <summary>
///     One sheet to create in a create_sheets batch.
/// </summary>
public class SheetSpec
{
    [JsonProperty("number")]
    public string Number { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}
