using PbLayla.Model;

namespace PbLayla.Repositories;

public interface IProfitTransferRepository
{
    public Task<string[]> FindMissingTransactionLogIdsAsync(IReadOnlyList<string> transactionLogIds, CancellationToken cancel = default);
    public Task<decimal> GetTransferDeficitAsync(CancellationToken cancel = default);
    public Task MarkTransactionLogsProcessedAsync(IReadOnlyList<TransactionLog> transactionLogs, 
        IDictionary<string, decimal> monthlyTotalProfitChanges,
        IDictionary<string, decimal> monthlyTransferredProfitChanges,
        IDictionary<string, decimal> monthlyRemainingProfitChanges,
        decimal transferDeficit,
        CancellationToken cancel = default);
}