namespace PbLayla.Model;

public class TransactionLog
{
    public required string Id { get; set; }
    public string? Symbol { get; set; } 
    public DateTime TransactionTime { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal? Funding { get; set; }
    public decimal? Fee { get; set; }
    public decimal? CashFlow { get; set; }
    public decimal? Change { get; set; }
    public decimal? CashBalance { get; set; }
}