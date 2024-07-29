namespace PbLayla.Repositories;

public class FileHedgeRecordRepositoryOptions
{
    public string AccountName { get; set; } = "Default";

    public string FileDirectory { get; set; } = "Data";

    public TimeSpan MaxHistory { get; set; } = TimeSpan.FromDays(14);
}