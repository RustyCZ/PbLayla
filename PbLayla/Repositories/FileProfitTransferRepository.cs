using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.Model;

namespace PbLayla.Repositories;

public class FileProfitTransferRepository : IProfitTransferRepository
{
    private readonly IOptions<FileProfitTransferRepositoryOptions> m_options;
    private readonly ILogger m_logger;
    private ProcessedProfitTransfer? m_processedProfitTransfer;
    private readonly string m_filePath;

    public FileProfitTransferRepository(IOptions<FileProfitTransferRepositoryOptions> options, ILogger logger)
    {
        m_options = options;
        m_logger = logger;
        string fileName = FormattableString.Invariant($"{options.Value.AccountName}_profit_transfers.json");
        m_filePath = Path.Combine(options.Value.FileDirectory, fileName);
    }

    public async Task<string[]> FindMissingTransactionLogIdsAsync(IReadOnlyList<string> transactionLogIds, CancellationToken cancel = default)
    {
        var processedLogs = await GetProcessedProfitTransferAsync(cancel);
        var missingIds = transactionLogIds.Except(processedLogs.ProcessedLogs.Select(l => l.Id)).ToArray();
        return missingIds;
    }

    public async Task<decimal> GetTransferDeficitAsync(CancellationToken cancel)
    {
        var processedLogs = await GetProcessedProfitTransferAsync(cancel);
        return processedLogs.TransferDeficit;
    }

    public async Task MarkTransactionLogsProcessedAsync(IReadOnlyList<TransactionLog> transactionLogs, 
        IDictionary<string, decimal> monthlyTotalProfitChanges,
        IDictionary<string, decimal> monthlyTransferredProfitChanges, 
        IDictionary<string, decimal> monthlyRemainingProfitChanges,
        decimal transferDeficit,
        CancellationToken cancel = default)
    {
        var processedLogs = await GetProcessedProfitTransferAsync(cancel);
        processedLogs.ProcessedLogs.AddRange(transactionLogs);
        processedLogs.ProcessedLogs = processedLogs.ProcessedLogs
            .DistinctBy(x => x.Id)
            .OrderByDescending(x => x.TransactionType)
            .ToList();
        processedLogs.TransferDeficit = transferDeficit;
        foreach (var (key, value) in monthlyTotalProfitChanges)
        {
            if (processedLogs.MonthlyTotalProfits.TryGetValue(key, out var monthlyTotalProfit))
                processedLogs.MonthlyTotalProfits[key] = monthlyTotalProfit + value;
            else
                processedLogs.MonthlyTotalProfits[key] = value;
            processedLogs.TotalProfit += value;
        }

        foreach (var (key, value) in monthlyTransferredProfitChanges)
        {
            if (processedLogs.MonthlyTransferredProfits.TryGetValue(key, out var monthlyTransferredProfit))
                processedLogs.MonthlyTransferredProfits[key] = monthlyTransferredProfit + value;
            else
                processedLogs.MonthlyTransferredProfits[key] = value;
            processedLogs.TotalTransferredProfit += value;
        }

        foreach (var (key, value) in monthlyRemainingProfitChanges)
        {
            if (processedLogs.MonthlyRemainingProfits.TryGetValue(key, out var monthlyRemainingProfit))
                processedLogs.MonthlyRemainingProfits[key] = monthlyRemainingProfit + value;
            else
                processedLogs.MonthlyRemainingProfits[key] = value;
        }

        var expiration = DateTime.UtcNow - m_options.Value.MaxTransactionLogsHistory;
        processedLogs.ProcessedLogs.RemoveAll(l => l.TransactionTime < expiration);
        var json = JsonSerializer.Serialize(processedLogs,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });
        await AtomicSaveFileAsync(json, cancel);
    }

    private string GetTempFile()
    {
        return m_filePath + ".tmp";
    }

    private void MoveTempFile(string tempFilePath)
    {
        if (!File.Exists(tempFilePath))
            return;
        if (File.Exists(m_filePath))
            File.Delete(m_filePath);
        File.Move(tempFilePath, m_filePath);
    }

    private void MoveTempFile()
    {
        MoveTempFile(GetTempFile());
    }

    private async Task AtomicSaveFileAsync(string json, CancellationToken cancel = default)
    {
        var tempFilePath = GetTempFile();
        await File.WriteAllTextAsync(tempFilePath, json, cancel);
        MoveTempFile(tempFilePath);
    }

    private async Task<ProcessedProfitTransfer> GetProcessedProfitTransferAsync(CancellationToken cancel = default)
    {
        try
        {
            if (m_processedProfitTransfer != null)
                return m_processedProfitTransfer;
            MoveTempFile();

            if (!File.Exists(m_filePath))
                return new ProcessedProfitTransfer();

            // json deserialization
            var json = await File.ReadAllTextAsync(m_filePath, cancel);
            var records = JsonSerializer.Deserialize<ProcessedProfitTransfer>(json) ?? new ProcessedProfitTransfer();
            m_processedProfitTransfer = records;
            
            return m_processedProfitTransfer;
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "Failed to read hedge records from file.");
            return new ProcessedProfitTransfer();
        }
    }

}