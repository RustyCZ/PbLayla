using System.Text.Json.Serialization;

namespace PbLayla.Model.PbConfig;

public class UniversalLiveConfig
{
    [JsonPropertyName("long")]
    public UniversalLiveConfigSide Long { get; set; } = new UniversalLiveConfigSide();

    [JsonPropertyName("short")]
    public UniversalLiveConfigSide Short { get; set; } = new UniversalLiveConfigSide();

    public UniversalLiveConfig Clone()
    {
        return new UniversalLiveConfig
        {
            Long = Long.Clone(),
            Short = Short.Clone()
        };
    }
}