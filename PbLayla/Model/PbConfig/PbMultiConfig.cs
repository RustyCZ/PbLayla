using System.Text.Json.Serialization;

namespace PbLayla.Model.PbConfig;

public class PbMultiConfig
{
    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("multisym_auto_unstuck_enabled")]
    public bool MultisymAutoUnstuckEnabled { get; set; }

    [JsonPropertyName("pnls_max_lookback_days")]
    public int PnlsMaxLookbackDays { get; set; }

    [JsonPropertyName("loss_allowance_pct")]
    public double LossAllowancePct { get; set; }

    [JsonPropertyName("stuck_threshold")]
    public double StuckThreshold { get; set; }

    [JsonPropertyName("unstuck_close_pct")]
    public double UnstuckClosePct { get; set; }

    [JsonPropertyName("auto_gs")]
    public bool AutoGs { get; set; }

    [JsonPropertyName("execution_delay_seconds")]
    public int ExecutionDelaySeconds { get; set; }

    [JsonPropertyName("TWE_long")]
    public double TweLong { get; set; }

    [JsonPropertyName("TWE_short")]
    public double TweShort { get; set; }

    [JsonPropertyName("symbols")]
    public SymbolConfig Symbols { get; set; } = new SymbolConfig();

    [JsonPropertyName("live_configs_dir")]
    public string LiveConfigsDir { get; set; } = string.Empty;

    [JsonPropertyName("default_config_path")] 
    public string DefaultConfigPath { get; set; } = string.Empty;

    public PbMultiConfig Clone()
    {
        return new PbMultiConfig
        {
            User = User,
            MultisymAutoUnstuckEnabled = MultisymAutoUnstuckEnabled,
            PnlsMaxLookbackDays = PnlsMaxLookbackDays,
            LossAllowancePct = LossAllowancePct,
            StuckThreshold = StuckThreshold,
            UnstuckClosePct = UnstuckClosePct,
            AutoGs = AutoGs,
            ExecutionDelaySeconds = ExecutionDelaySeconds,
            TweLong = TweLong,
            TweShort = TweShort,
            Symbols = Symbols.Clone(),
            LiveConfigsDir = LiveConfigsDir,
            DefaultConfigPath = DefaultConfigPath
        };
    }
}