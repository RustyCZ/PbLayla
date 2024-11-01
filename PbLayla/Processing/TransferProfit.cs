using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.Exchanges;
using PbLayla.Model;
using PbLayla.Repositories;

namespace PbLayla.Processing;

public class TransferProfit : ITransferProfit
{
    private readonly IPbFuturesRestClient m_client;
    private readonly IOptions<TransferProfitOptions> m_options;
    private readonly IProfitTransferRepository m_profitTransferRepository;
    private readonly ILogger<TransferProfit> m_logger;

    public TransferProfit(IOptions<TransferProfitOptions> options,
        IPbFuturesRestClient client, 
        IProfitTransferRepository profitTransferRepository, 
        ILogger<TransferProfit> logger)
    {
        m_client = client;
        m_profitTransferRepository = profitTransferRepository;
        m_logger = logger;
        m_options = options;
    }

    public async Task ExecuteAsync(CancellationToken cancel = default)
    {
        try
        {
            m_logger.LogInformation("{AccountName}: Executing profit transfers", m_options.Value.AccountName);
            await ExecuteInnerAsync(cancel);
            m_logger.LogInformation("{AccountName}: Profit transfers executed", m_options.Value.AccountName);
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "Error executing profit transfers");
        }
    }

    private async Task ExecuteInnerAsync(CancellationToken cancel)
    {
        var end = DateTime.UtcNow;
        var start = end - m_options.Value.MaxLookBack;
        var transactionLogs = await m_client.GetTransactionLogsAsync(start, end, cancel);
        var transactionLogIds = transactionLogs.Select(log => log.Id).ToArray();
        var missingIds = await m_profitTransferRepository.FindMissingTransactionLogIdsAsync(transactionLogIds, cancel);
        var transferDeficit = await m_profitTransferRepository.GetTransferDeficitAsync(cancel);
        m_logger.LogInformation("{AccountName}: Found {MissingCount} missing transaction logs", m_options.Value.AccountName, missingIds.Length);
        m_logger.LogInformation("{AccountName}: Transfer deficit is {TransferDeficit}", m_options.Value.AccountName, transferDeficit);
        var missingIdsSet = new HashSet<string>(missingIds);
        var missingLogs = transactionLogs.Where(log => missingIdsSet.Contains(log.Id)).ToArray();
        var monthlyTotalProfitChanges = new Dictionary<string, decimal>();
        var monthlyTransferredProfitChanges = new Dictionary<string, decimal>();
        var monthlyRemainingProfitChanges = new Dictionary<string, decimal>();

        foreach (var log in missingLogs)
        {
            if (log.Change == null)
                continue;
            if (log.TransactionType != TransactionType.Trade
                && log.TransactionType != TransactionType.Settlement
                && log.TransactionType != TransactionType.Liquidation)
                continue;
            var monthKey = GetMonthKey(log.TransactionTime);
            if (monthlyTotalProfitChanges.TryGetValue(monthKey, out var monthlyTotal))
                monthlyTotal += log.Change.Value;
            else
                monthlyTotal = log.Change.Value;
            monthlyTotalProfitChanges[monthKey] = monthlyTotal;
        }

        var monthlyProfitChangesOrdered = monthlyTotalProfitChanges.OrderBy(x => x.Key);
        foreach (var keyValuePair in monthlyProfitChangesOrdered)
        {
            var monthKey = keyValuePair.Key;
            var profitChange = keyValuePair.Value;
            transferDeficit += profitChange;
            decimal remainingChange;
            decimal transferredChange;
            if (transferDeficit > 0)
            {
                // we can transfer profit
                transferredChange = Math.Round(transferDeficit * m_options.Value.TransferProfitRatio, 4);
                remainingChange = profitChange - transferredChange;
                transferDeficit = 0;
            }
            else
            {
                // everything needs to stay in the account to cover the deficit
                transferredChange = 0;
                remainingChange = profitChange;
            }
            monthlyTransferredProfitChanges[monthKey] = transferredChange;
            monthlyRemainingProfitChanges[monthKey] = remainingChange;
        }

        if (monthlyTransferredProfitChanges.Any())
        {
            var totalTransferChange = monthlyTransferredProfitChanges.Values.Sum();
            if (totalTransferChange > 0)
            {
                string transferFrom = m_options.Value.TransferProfitFrom;
                string transferTo = m_options.Value.TransferProfitTo;
                m_logger.LogInformation("{AccountName}: Transferring '{TotalTransferChange}' profit from '{From}/UTA' '{To}/funding'", 
                    m_options.Value.AccountName, 
                    totalTransferChange,
                    transferFrom,
                    transferTo);
                await m_client.TransferProfitAsync(totalTransferChange, transferFrom, transferTo, cancel);
                m_logger.LogInformation(
                    "{AccountName}: Transferred '{TotalTransferChange}' profit from '{From}/UTA' '{To}/funding'",
                    m_options.Value.AccountName,
                    totalTransferChange,
                    transferFrom,
                    transferTo);
            }
            else
            {
                m_logger.LogInformation("{AccountName}: No profit to transfer", m_options.Value.AccountName);
            }
        }
        
        m_logger.LogInformation("{AccountName}: Marking {TransactionCount} transaction logs as processed", m_options.Value.AccountName, transactionLogs.Length);
        await m_profitTransferRepository.MarkTransactionLogsProcessedAsync(transactionLogs,
            monthlyTotalProfitChanges,
            monthlyTransferredProfitChanges,
            monthlyRemainingProfitChanges,
            transferDeficit,
            cancel);
        m_logger.LogInformation("{AccountName}: Marked {TransactionCount} transaction logs as processed", m_options.Value.AccountName, transactionLogs.Length);
    }

    private static string GetMonthKey(DateTime date)
    {
        return date.ToString("yyyy-MM");
    }
}