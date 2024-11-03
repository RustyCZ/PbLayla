namespace PbLayla.Configuration;

public class RiskMonitorConfig
{
    public TimeSpan ExecutionInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan AllowedExecutionDelay { get; set; } = TimeSpan.FromMinutes(1);
}