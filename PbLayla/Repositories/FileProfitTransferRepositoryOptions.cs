namespace PbLayla.Repositories;

public class FileProfitTransferRepositoryOptions
{
    public TimeSpan MaxTransactionLogsHistory { get; set; } = TimeSpan.FromDays(30);
    public string AccountName { get; set; } = "Default";
    public string FileDirectory { get; set; } = "Data";
}