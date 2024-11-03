namespace PbLayla.HealthChecks;

public class BackgroundExecutionLastStateProviderOptions
{
    public TimeSpan RiskMonitorExecutionInterval { get; set; }
    public TimeSpan AllowedExecutionDelay { get; set; }
}