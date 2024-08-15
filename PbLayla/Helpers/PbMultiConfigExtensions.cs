using System.Text.Json;
using Hjson;
using PbLayla.Model.PbConfig;

namespace PbLayla.Helpers;

public static class PbMultiConfigExtensions
{
    public static string GenerateNormalConfig(this PbMultiConfig template)
    {
        var config = template.Clone();
        var serializedConfig = config.SerializeConfig();
        return serializedConfig;
    }

    public static string GenerateUnstuckConfig(this PbMultiConfig template, HashSet<string> symbolsToUnstuck, string unstuckConfig, double unstuckExposure, bool disableOthers)
    {
        var config = template.Clone();
        var symbols = config.Symbols.ParseSymbols();
        var foundSymbols = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var symbolConfig in symbols)
        {
            if (symbolsToUnstuck.Contains(symbolConfig.Symbol))
            {
                foundSymbols.Add(symbolConfig.Symbol);
                symbolConfig.LongMode = TradeMode.GracefulStop;
                symbolConfig.LiveConfigPath = FormattableString.Invariant($"configs/{unstuckConfig}");
                symbolConfig.WalletExposureLimitLong = unstuckExposure;
            }
            else
            {
                if (disableOthers && symbolConfig.LongMode is TradeMode.GracefulStop or TradeMode.Normal)
                    symbolConfig.LongMode = TradeMode.TakeProfitOnly;
            }
        }

        // add stuck symbols that we have not found in config
        foreach (var symbolToUnstuck in symbolsToUnstuck)
        {
            if (!foundSymbols.Contains(symbolToUnstuck))
            {
                var newSymbol = new SymbolOptions
                {
                    LongMode = TradeMode.GracefulStop,
                    LiveConfigPath = FormattableString.Invariant($"configs/{unstuckConfig}"),
                    Symbol = symbolToUnstuck,
                    WalletExposureLimitLong = unstuckExposure,
                    ShortMode = TradeMode.Manual,
                };
                symbols = symbols.Append(newSymbol).ToArray();
            }
        }

        config.Symbols.UpdateSymbols(symbols);

        var serializedConfig = config.SerializeConfig();
        return serializedConfig;
    }

    public static string SerializeConfig(this PbMultiConfig config)
    {
        string jsonString = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        JsonValue v = JsonValue.Parse(jsonString);
        var serializedConfig = v.ToString(Stringify.Hjson);
        return serializedConfig;
    }
}
