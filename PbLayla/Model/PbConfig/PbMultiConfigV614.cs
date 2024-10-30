using Hjson;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PbLayla.Model.PbConfig;

public class PbMultiConfigV614 : IPbMultiConfig
{
    [JsonPropertyName("user")]
    public string User { get; set; } = "bybit_01";

    [JsonPropertyName("pnls_max_lookback_days")]
    public int PnlsMaxLookbackDays { get; set; } = 30;

    [JsonPropertyName("loss_allowance_pct")]
    public double LossAllowancePct { get; set; } = 0.005;

    [JsonPropertyName("stuck_threshold")]
    public double StuckThreshold { get; set; } = 0.89;

    [JsonPropertyName("unstuck_close_pct")]
    public double UnstuckClosePct { get; set; } = 0.005;

    [JsonPropertyName("execution_delay_seconds")]
    public int ExecutionDelaySeconds { get; set; } = 2;

    [JsonPropertyName("max_n_cancellations_per_batch")]
    public int MaxNCancellationsPerBatch { get; set; } = 8;

    [JsonPropertyName("max_n_creations_per_batch")]
    public int MaxNCreationsPerBatch { get; set; } = 4;

    [JsonPropertyName("price_distance_threshold")]
    public double PriceDistanceThreshold { get; set; } = 0.002;

    [JsonPropertyName("filter_by_min_effective_cost")]
    public bool FilterByMinEffectiveCost { get; set; }

    [JsonPropertyName("auto_gs")]
    public bool AutoGs { get; set; }

    [JsonPropertyName("leverage")]
    public double Leverage { get; set; } = 10.0;

    [JsonPropertyName("TWE_long")]
    public double TweLong { get; set; }

    [JsonPropertyName("TWE_short")]
    public double TweShort { get; set; }

    [JsonPropertyName("long_enabled")]
    public bool LongEnabled { get; set; } = true;

    [JsonPropertyName("short_enabled")]
    public bool ShortEnabled { get; set; } = false;

    [JsonPropertyName("approved_symbols")]
    public SymbolConfig Symbols { get; set; } = new SymbolConfig();

    [JsonPropertyName("forced_mode_long")]
    public string ForcedModeLong { get; set; } = string.Empty;

    [JsonPropertyName("forced_mode_short")]
    public string ForcedModeShort { get; set; } = string.Empty;

    [JsonPropertyName("live_configs_dir")]
    public string LiveConfigsDir { get; set; } = "configs/live/multisymbol/no_AU/";

    [JsonPropertyName("default_config_path")]
    public string DefaultConfigPath { get; set; } = "configs/live/recursive_grid_mode.example.json";

    [JsonPropertyName("universal_live_config")]
    public UniversalLiveConfig UniversalLiveConfig { get; set; } = new UniversalLiveConfig();

    [JsonPropertyName("ignored_symbols")]
    public string[] IgnoredSymbols { get; set; } = [];

    [JsonPropertyName("n_longs")]
    public int NLongs { get; set; }

    [JsonPropertyName("n_shorts")]
    public int NShorts { get; set; }

    [JsonPropertyName("minimum_market_age_days")]
    public int MinimumMarketAgeDays { get; set; } = 30;

    [JsonPropertyName("ohlcv_interval")]
    public string OhlcvInterval { get; set; } = "15m";

    [JsonPropertyName("n_ohlcvs")]
    public int NOhlcvs { get; set; } = 100;

    [JsonPropertyName("relative_volume_filter_clip_pct")]
    public double RelativeVolumeFilterClipPct { get; set; } = 0.1;

    public int GetSymbolCount()
    {
        return Symbols.Count;
    }

    public IPbMultiConfig Clone()
    {
        return new PbMultiConfigV614
        {
            User = User,
            PnlsMaxLookbackDays = PnlsMaxLookbackDays,
            LossAllowancePct = LossAllowancePct,
            StuckThreshold = StuckThreshold,
            UnstuckClosePct = UnstuckClosePct,
            ExecutionDelaySeconds = ExecutionDelaySeconds,
            MaxNCancellationsPerBatch = MaxNCancellationsPerBatch,
            MaxNCreationsPerBatch = MaxNCreationsPerBatch,
            PriceDistanceThreshold = PriceDistanceThreshold,
            FilterByMinEffectiveCost = FilterByMinEffectiveCost,
            AutoGs = AutoGs,
            Leverage = Leverage,
            TweLong = TweLong,
            TweShort = TweShort,
            LongEnabled = LongEnabled,
            ShortEnabled = ShortEnabled,
            Symbols = Symbols.Clone(),
            ForcedModeLong = ForcedModeLong,
            ForcedModeShort = ForcedModeShort,
            LiveConfigsDir = LiveConfigsDir,
            DefaultConfigPath = DefaultConfigPath,
            UniversalLiveConfig = UniversalLiveConfig.Clone(),
            IgnoredSymbols = IgnoredSymbols,
            NLongs = NLongs,
            NShorts = NShorts,
            MinimumMarketAgeDays = MinimumMarketAgeDays,
            OhlcvInterval = OhlcvInterval,
            NOhlcvs = NOhlcvs,
            RelativeVolumeFilterClipPct = RelativeVolumeFilterClipPct
        };
    }

    public string SerializeConfig()
    {
        string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        JsonValue v = JsonValue.Parse(jsonString);
        var serializedConfig = v.ToString(Stringify.Hjson);
        return serializedConfig;
    }

    public void UpdateSymbols(SymbolOptions[] symbols)
    {
        Symbols.UpdateSymbols(symbols);
    }

    public double GetTweLong()
    {
        return TweLong;
    }

    public SymbolOptions[] ParseSymbols()
    {
        return Symbols.ParseSymbols();
    }
}