namespace PbLayla.Services;

public class MonitorServiceOptions
{
    public TimeSpan ExecutionInterval { get; set; } = TimeSpan.FromSeconds(5);
}