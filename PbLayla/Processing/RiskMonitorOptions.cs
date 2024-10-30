using PbLayla.Configuration;

namespace PbLayla.Processing;

public class RiskMonitorOptions
{
    public string AccountName { get; set; } = string.Empty;

    public string ConfigTemplateFileName { get; set; } = string.Empty;

    public double StuckExposureRatio { get; set; } = 0.95;

    public TimeSpan MinStuckTime { get; set; } = TimeSpan.FromHours(3);

    public double StageOneTotalStuckExposure { get; set; } = 1.0;

    public TimeSpan StateChangeCheckTime { get; set; }= TimeSpan.FromMinutes(5);

    public string ConfigsPath { get; set; } = string.Empty;
    
    public double OverExposeFilterFactor { get; set; } = 1.1;

    public string UnstuckConfig { get; set; } = string.Empty;

    public string DoriConfig { get; set; } = string.Empty;

    public double UnstuckExposure { get; set; } = 1.0;

    public bool DisableOthersWhileUnstucking { get; set; }

    public double PriceDistanceStuck { get; set; } = 0.05;
    
    public double PriceDistanceCloseHedge { get; set; } = 0.04;

    public double PriceDistanceUnstuckStuck { get; set; } = 0.1;

    public double PriceDistanceUnstuckCloseHedge { get; set; } = 0.09;

    public int MaxHedgeReleaseAttempts { get; set; } = 30;

    public int MaxUnstuckSymbols { get; set; } = 1;

    public TimeSpan MaxHedgeReleaseAttemptsPeriod { get; set; } = TimeSpan.FromHours(24);

    public bool ManagePbLifecycle { get; set; } = true;

    public bool ManageHedges { get; set; } = true;

    public bool ManageDori { get; set; }

    public double InitialQtyPercent { get; set; } = 0.01;

    public bool CopyTrading { get; set; }

    public string[] ManualHedgeSymbols { get; set; } = [];

    public PbVersion PbVersion { get; set; } = PbVersion.V610;
}