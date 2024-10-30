using System.Text.Json;
using Hjson;
using JetBrains.Annotations;
using PbLayla.Model.PbConfig;
using Xunit;

namespace PbLayla.Tests.Model.PbConfig;

[TestSubject(typeof(PbMultiConfig))]
public class PbMultiConfigTest
{

    [Fact(Skip = "Manual")]
    public void TryToLoadConfig()
    {
        const string configFile = "test.hjson";
        var jsonString = HjsonValue.Load(configFile).ToString();
        var config = JsonSerializer.Deserialize<PbMultiConfig>(jsonString);
        var symbols = config.Symbols.ParseSymbols();
        config.Symbols.UpdateSymbols(symbols);
        string jsonString2 = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        JsonValue v = JsonValue.Parse(jsonString2);
        var hjson = v.ToString(Stringify.Hjson);
        Assert.NotNull(hjson);
    }

    [Fact(Skip = "Manual")]
    public void TryToLoadConfigV614()
    {
        const string configFile = "test.hjson";
        var jsonString = HjsonValue.Load(configFile).ToString();
        var config = JsonSerializer.Deserialize<PbMultiConfigV614>(jsonString);
        var symbols = config.Symbols.ParseSymbols();
        config.Symbols.UpdateSymbols(symbols);
        string jsonString2 = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        JsonValue v = JsonValue.Parse(jsonString2);
        var hjson = v.ToString(Stringify.Hjson);
        Assert.NotNull(hjson);
    }
}