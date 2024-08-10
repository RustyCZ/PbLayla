﻿namespace PbLayla.Configuration
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
        public TimeSpan StateChangeCheckTime { get; set; }= TimeSpan.FromMinutes(2);
        public string ConfigsPath { get; set; } = string.Empty;
        public double OverExposeFilterFactor { get; set; } = 1.1;
        public string UnstuckConfig { get; set; } = string.Empty;
        public double UnstuckExposure { get; set; } = 1.0;
        public bool DisableOthersWhileUnstucking { get; set; }
        public double PriceDistanceStuck { get; set; } = 0.05;
        public double PriceDistanceCloseHedge { get; set; } = 0.04;
        public double PriceDistanceUnstuckStuck { get; set; } = 0.1;
        public double PriceDistanceUnstuckCloseHedge { get; set; } = 0.09;
        public int MaxHedgeReleaseAttempts { get; set; } = 30;
        public TimeSpan MaxHedgeReleaseAttemptsPeriod { get; set; } = TimeSpan.FromHours(24);
        public int MaxUnstuckSymbols { get; set; } = 1;
    }
}
