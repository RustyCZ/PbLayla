using System.Text.Json.Serialization;

namespace PbLayla.Model.Dori;

public class MarketTrendApiResult
{
    [JsonPropertyName("market_trend")]
    public MarketTrend? MarketTrend { get; set; }
    [JsonPropertyName("data_available")]
    public bool DataAvailable { get; set; }
}