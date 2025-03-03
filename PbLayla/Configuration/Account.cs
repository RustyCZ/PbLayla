namespace PbLayla.Configuration
{
    public class Account
    {
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public Exchange Exchange { get; set; }
        public bool IsUnified { get; set; }
        public string ConfigTemplateFileName { get; set; } = string.Empty;
        public double StuckExposureRatio { get; set; } = 0.95;
        public TimeSpan MinStuckTime { get; set; } = TimeSpan.FromHours(3);
        public double StageOneTotalStuckExposure { get; set; } = 1.0;
        public TimeSpan StateChangeCheckTime { get; set; }= TimeSpan.FromMinutes(5);
        public string ConfigsPath { get; set; } = string.Empty;
        public double OverExposeFilterFactor { get; set; } = 1.1;
        public string UnstuckConfig { get; set; } = string.Empty;
        public string DoriConfig { get; set; } = string.Empty;
        public string CautiousDoriConfig { get; set; } = string.Empty;
        public string CautiousUnstuckConfig { get; set; } = string.Empty;
        public double UnstuckExposure { get; set; } = 1.0;
        public double NormalPbStuckThreshold { get; set; } = 0.97;
        public double FastReducePbStuckThreshold { get; set; } = 0.3;
        public double NormalPbLossAllowance { get; set; } = 0.01;
        public double FastReducePbLossAllowance { get; set; } = 0.05;
        public bool DisableOthersWhileUnstucking { get; set; }
        public double PriceDistanceStuck { get; set; } = 0.12;
        public double PriceDistanceCloseHedge { get; set; } = 0.118;
        public double PriceDistanceUnstuckStuck { get; set; } = 0.2;
        public double PriceDistanceUnstuckCloseHedge { get; set; } = 0.198;
        public double CautiousDistanceStuck { get; set; } = 0.25;
        public double CautiousDistanceCloseHedge { get; set; } = 0.248;
        public double CautiousDistanceUnstuckStuck { get; set; } = 0.3;
        public double CautiousDistanceUnstuckCloseHedge { get; set; } = 0.298;
        public int MaxHedgeReleaseAttempts { get; set; } = 30;
        public TimeSpan MaxHedgeReleaseAttemptsPeriod { get; set; } = TimeSpan.FromHours(24);
        public int MaxUnstuckSymbols { get; set; } = 1;
        public bool ManagePbLifecycle { get; set; } = true;
        public bool ManageHedges { get; set; } = true;
        public bool ManageDori { get; set; }
        /// <summary>
        /// Gets or sets the initial quantity percent. It is needed only for Dori service
        /// </summary>
        public double InitialQtyPercent { get; set; } = 0.01;
        public bool CopyTrading { get; set; }
        public string[] ManualHedgeSymbols { get; set; } = [];
        public bool EnableProfitTransfer { get; set; }
        public decimal TransferProfitRatio { get; set; } = 0.25m;
        public string TransferProfitFrom { get; set; } = string.Empty;
        public string TransferProfitTo { get; set; } = string.Empty;
        public TimeSpan TransferProfitLookBack { get; set; } = TimeSpan.FromDays(3);
        public TimeSpan TransferProfitLogHistory { get; set; } = TimeSpan.FromDays(30);
        public PbVersion PbVersion { get; set; } = PbVersion.V610;
        public bool MarketTrendAdaptive { get; set; }
    }
}
