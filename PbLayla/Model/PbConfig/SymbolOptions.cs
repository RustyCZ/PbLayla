using System.Globalization;

namespace PbLayla.Model.PbConfig;

public class SymbolOptions
{
    public string Symbol { get; set; } = string.Empty;
    public string LiveConfigPath { get; set; } = string.Empty;
    public TradeMode LongMode { get; set; } = TradeMode.None;
    public TradeMode ShortMode { get; set; } = TradeMode.None;
    public double? PricePrecisionMultiplier { get; set; }
    public double? PriceStepCustom { get; set; }
    public double? WalletExposureLimitLong { get; set; }
    public double? WalletExposureLimitShort { get; set; }
    public double? LeverageSetOnExchange { get; set; }

    public static SymbolOptions FromFlags(string symbol, string flags)
    {
        string[] flagParts = flags.Split('-');
        var symbolOptions = new SymbolOptions
        {
            Symbol = symbol
        };
        foreach (var flagPart in flagParts)
        {
            if (string.IsNullOrWhiteSpace(flagPart))
                continue;
            var parts = flagPart.Split(' ')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            if (parts.Length != 2)
                throw new InvalidOperationException(FormattableString.Invariant($"Invalid flag part: {flagPart}"));
            var key = parts.First();
            var value = parts.Last();
            switch (key)
            {
                case "lm":
                    symbolOptions.LongMode = ParseTradeMode(value);
                    break;
                case "sm":
                    symbolOptions.ShortMode = ParseTradeMode(value);
                    break;
                case "pp":
                    symbolOptions.PricePrecisionMultiplier = double.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
                    break;
                case "ps":
                    symbolOptions.PriceStepCustom = double.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
                    break;
                case "lw":
                    symbolOptions.WalletExposureLimitLong = double.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
                    break;
                case "sw":
                    symbolOptions.WalletExposureLimitShort = double.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
                    break;
                case "lev":
                    symbolOptions.LeverageSetOnExchange = double.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
                    break;
                case "lc": 
                    symbolOptions.LiveConfigPath = value;
                    break;
                default:
                    throw new InvalidOperationException(FormattableString.Invariant($"Invalid flag key: {key}"));
            }
        }
        return symbolOptions;
    }

    public string ToFlags()
    {
        var flags = new List<string>();
        if (LongMode != TradeMode.None)
            flags.Add(FormattableString.Invariant($"-lm {ToFlags(LongMode)}"));
        if (ShortMode != TradeMode.None)
            flags.Add(FormattableString.Invariant($"-sm {ToFlags(ShortMode)}"));
        if (PricePrecisionMultiplier.HasValue)
            flags.Add(FormattableString.Invariant($"-pp {PricePrecisionMultiplier.Value:0.########}"));
        if (PriceStepCustom.HasValue)
            flags.Add(FormattableString.Invariant($"-ps {PriceStepCustom.Value:0.########}"));
        if (WalletExposureLimitLong.HasValue)
            flags.Add(FormattableString.Invariant($"-lw {WalletExposureLimitLong.Value:0.########}"));
        if (WalletExposureLimitShort.HasValue)
            flags.Add(FormattableString.Invariant($"-sw {WalletExposureLimitShort.Value:0.########}"));
        if (LeverageSetOnExchange.HasValue)
            flags.Add(FormattableString.Invariant($"-lev {LeverageSetOnExchange.Value:0.########}"));
        if (!string.IsNullOrWhiteSpace(LiveConfigPath))
            flags.Add(FormattableString.Invariant($"-lc {LiveConfigPath}"));
        return string.Join(" ", flags);
    }

    private static TradeMode ParseTradeMode(string value)
    {
        switch (value)
        {
            case "n":
                return TradeMode.Normal;
            case "m":
                return TradeMode.Manual;
            case "gs":
                return TradeMode.GracefulStop;
            case "p":
                return TradeMode.Panic;
            case "t":
                return TradeMode.TakeProfitOnly;
            default:
                throw new InvalidOperationException(FormattableString.Invariant($"Invalid trade mode: {value}"));
        }
    }

    private static string ToFlags(TradeMode mode)
    {
        switch (mode)
        {
            case TradeMode.Normal:
                return "n";
            case TradeMode.Manual:
                return "m";
            case TradeMode.GracefulStop:
                return "gs";
            case TradeMode.Panic:
                return "p";
            case TradeMode.TakeProfitOnly:
                return "t";
            default:
                throw new InvalidOperationException(FormattableString.Invariant($"Invalid trade mode: {mode}"));
        }
    }
}