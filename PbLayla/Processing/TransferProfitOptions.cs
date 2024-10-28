namespace PbLayla.Processing;

public class TransferProfitOptions
{
    public string AccountName { get; set; } = string.Empty;
    public TimeSpan MaxLookBack { get; set; } = TimeSpan.FromDays(7);
    public decimal TransferProfitRatio { get; set; } = 0.25m;
    public string TransferProfitFrom { get; set; } = string.Empty;
    public string TransferProfitTo { get; set; } = string.Empty;
}