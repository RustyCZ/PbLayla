namespace PbLayla.Services;

public class TransferProfitServiceOptions
{
    public TimeSpan ExecutionInterval { get; set; } = TimeSpan.FromHours(1);
}