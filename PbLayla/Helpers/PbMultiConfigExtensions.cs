using System.Text.Json;
using Hjson;
using Microsoft.Extensions.Logging;
using PbLayla.Model.Dori;
using PbLayla.Model.PbConfig;

namespace PbLayla.Helpers;

public static class PbMultiConfigExtensions
{
    public static string GenerateNormalConfig(this IPbMultiConfig template)
    {
        var config = template.Clone();
        var serializedConfig = config.SerializeConfig();
        return serializedConfig;
    }

    public static string GenerateUnstuckConfig(this IPbMultiConfig template, HashSet<string> symbolsToUnstuck, string unstuckConfig, double unstuckExposure, bool disableOthers)
    {
        var config = template.Clone();
        var symbols = config.ParseSymbols();
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

        config.UpdateSymbols(symbols);

        var serializedConfig = config.SerializeConfig();
        return serializedConfig;
    }

    public static string UpdateDoriTemplateConfig(this IPbMultiConfig template, 
        RiskModel riskModel,
        StrategyApiResult strategyResult, 
        string accountName,
        string unstuckConfig,
        string doriConfig,
        ILogger logger)
    {
        // take existing template and current open positions
        // foreach symbol in template
        // if symbol is in manual position, always copy
        // if symbol is in normal position and not in desired symbol list set it to graceful shutdown
        // if symbol is in normal position and in desired symbol list, copy the symbol
        // if symbol is in graceful shutdown and in desired symbol list, copy the symbol and set it to normal position
        // if symbol is in graceful shutdown and not in desired symbol list and no open position mark it as replaceable
        // if symbol is in graceful shutdown and not in desired symbol list and open position, copy the symbol with graceful shutdown

        // if we have replaceable symbols
        // foreach desired symbol ordered by adg
        // if it is not in the final list replace the symbol and remove it from replaceable list
        // if there is no more replaceable symbols break

        var config = template.Clone();
        var symbols = config.ParseSymbols();
        List<SymbolOptions> finalSymbols = new List<SymbolOptions>();
        HashSet<string> desiredSymbols = new HashSet<string>(strategyResult.SymbolData.Select(s => s.Symbol), StringComparer.InvariantCultureIgnoreCase);
        HashSet<string> openPositions = new HashSet<string>(riskModel.LongPositions.Keys, StringComparer.InvariantCultureIgnoreCase);
        HashSet<string> replaceableSymbols = new HashSet<string>();
        foreach (var symbol in symbols)
        {
            if(symbol.LongMode == TradeMode.Manual)
            {
                // in template manual can be configured only intentionally
                finalSymbols.Add(symbol);
                continue;
            }

            if (symbol.LongMode == TradeMode.Panic)
            {
                // in template panic can be configured only intentionally
                finalSymbols.Add(symbol);
                continue;
            }

            if (symbol.LongMode == TradeMode.TakeProfitOnly)
            {
                // in template take profit only can be configured only intentionally
                // take profit can be configured dynamically only in live config
                finalSymbols.Add(symbol);
                continue;
            }

            if (symbol.LongMode == TradeMode.Normal && !desiredSymbols.Contains(symbol.Symbol))
            {
                logger.LogInformation("{AccountName}: Setting '{Symbol}' to graceful stop", accountName, symbol.Symbol);
                symbol.LongMode = TradeMode.GracefulStop;
                symbol.LiveConfigPath = FormattableString.Invariant($"configs/{unstuckConfig}");
                finalSymbols.Add(symbol);
                continue;
            }

            if (symbol.LongMode == TradeMode.Normal && desiredSymbols.Contains(symbol.Symbol))
            {
                symbol.LiveConfigPath = FormattableString.Invariant($"configs/{doriConfig}");
                finalSymbols.Add(symbol);
                continue;
            }

            if (symbol.LongMode == TradeMode.GracefulStop && desiredSymbols.Contains(symbol.Symbol))
            {
                logger.LogInformation("{AccountName}: Setting '{Symbol}' to normal", accountName, symbol.Symbol);
                symbol.LongMode = TradeMode.Normal;
                symbol.LiveConfigPath = FormattableString.Invariant($"configs/{doriConfig}");
                finalSymbols.Add(symbol);
                continue;
            }

            if (symbol.LongMode == TradeMode.GracefulStop && !desiredSymbols.Contains(symbol.Symbol))
            {
                if (!openPositions.Contains(symbol.Symbol))
                {
                    logger.LogInformation("{AccountName}: Marking '{Symbol}' as replaceable", accountName, symbol.Symbol);
                    replaceableSymbols.Add(symbol.Symbol);
                    finalSymbols.Add(symbol);
                }
                else
                {
                    symbol.LiveConfigPath = FormattableString.Invariant($"configs/{unstuckConfig}");
                    finalSymbols.Add(symbol);
                }
            }
        }

        var desiredSymbolsOrderedByPerformance = strategyResult.SymbolData
            .OrderByDescending(s => s.BackTestResult.Result.AdgLong)
            .Select(s => s.Symbol)
            .ToList();
        while (replaceableSymbols.Count > 0)
        {
            bool replaced = false;
            foreach (var symbol in desiredSymbolsOrderedByPerformance)
            {
                bool isInFinalSymbols = finalSymbols.Any(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                if (isInFinalSymbols)
                    continue;
                var firstReplaceable = replaceableSymbols.First();
                var replaceableSymbol = finalSymbols.FirstOrDefault(s => s.Symbol.Equals(firstReplaceable, StringComparison.OrdinalIgnoreCase));
                if (replaceableSymbol != null)
                {
                    logger.LogInformation("{AccountName}: Replacing '{ReplaceableSymbol}' with '{NewSymbol}'", accountName, replaceableSymbol.Symbol, symbol);
                    replaceableSymbol.Symbol = symbol;
                    replaceableSymbol.LongMode = TradeMode.Normal;
                    replaceableSymbol.LiveConfigPath = FormattableString.Invariant($"configs/{doriConfig}");
                    replaceableSymbols.Remove(firstReplaceable);
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
                break;
        }

        config.UpdateSymbols(finalSymbols.ToArray());
        var serializedConfig = config.SerializeConfig();
        return serializedConfig;
    }
}
