using PbLayla.Model;

namespace PbLayla.Repositories;

public class ProcessedProfitTransfer
{
    public Dictionary<string, decimal> MonthlyTotalProfits { get; set; } = new Dictionary<string, decimal>();
    public Dictionary<string, decimal> MonthlyTransferredProfits { get; set; } = new Dictionary<string, decimal>();
    public Dictionary<string, decimal> MonthlyRemainingProfits { get; set; } = new Dictionary<string, decimal>();
    public List<TransactionLog> ProcessedLogs { get; set; } = new List<TransactionLog>();
    public decimal TotalProfit { get; set; }
    public decimal TotalTransferredProfit { get; set; }
    public decimal TransferDeficit { get; set; }
}