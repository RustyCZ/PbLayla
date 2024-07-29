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

    public static string GenerateUnstuckConfig(this PbMultiConfig template, string symbolToUnstuck, string unstuckConfig, double unstuckExposure, bool disableOthers)
    {
        var config = template.Clone();
        var symbols = config.Symbols.ParseSymbols();
        bool found = false;
        foreach (var symbolConfig in symbols)
        {
            if (string.Equals(symbolToUnstuck, symbolConfig.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                symbolConfig.LongMode = TradeMode.GracefulStop;
                symbolConfig.LiveConfigPath = FormattableString.Invariant($"configs/{unstuckConfig}");
                symbolConfig.WalletExposureLimitLong = unstuckExposure;
            }
            else
            {
                if(disableOthers)
                    symbolConfig.LongMode = TradeMode.TakeProfitOnly;
            }
        }

        if (!found)
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
