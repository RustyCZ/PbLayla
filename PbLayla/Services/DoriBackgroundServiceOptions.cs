namespace PbLayla.Services;

public class DoriBackgroundServiceOptions
{
    public TimeSpan ExecutionInterval { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan ExecutionFailInterval { get; set; } = TimeSpan.FromMinutes(1);
    public string[] Strategies { get; set; } = [];
}