using System.Text.Json.Serialization;

namespace PbLayla.Model.Dori;

public class SymbolTrend
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
    [JsonPropertyName("trend")]
    public Trend Trend { get; set; }
}