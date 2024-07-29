using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.Model;

namespace PbLayla.Repositories;

public class FileHedgeRecordRepository : IHedgeRecordRepository
{
    private readonly IOptions<FileHedgeRecordRepositoryOptions> m_options; 
    private readonly ILogger m_logger;
    private HedgeRecord[]? m_records;
    private readonly string m_filePath;

    public FileHedgeRecordRepository(IOptions<FileHedgeRecordRepositoryOptions> options, ILogger logger)
    {
        m_options = options;
        m_logger = logger;
        string fileName = FormattableString.Invariant($"{options.Value.AccountName}_hedge_records.json");
        m_filePath = Path.Combine(options.Value.FileDirectory, fileName);
        if (!Directory.Exists(options.Value.FileDirectory))
            Directory.CreateDirectory(options.Value.FileDirectory);
    }

    public async Task AddClosedHedgePositionAsync(Position position, CancellationToken cancel = default)
    {
        var records = await GetRecordsAsync(cancel);
        var utcNow = DateTime.UtcNow;
        var newRecords = records
            .Where(r => utcNow - r.Closed < m_options.Value.MaxHistory)
            .ToArray();
        var newRecord = new HedgeRecord
        {
            ClosedHedgePosition = position,
            Closed = DateTime.UtcNow
        };
        newRecords = newRecords.Append(newRecord).ToArray();
        m_records = newRecords;
        try
        {
            var json = JsonSerializer.Serialize(newRecords, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            if (File.Exists(m_filePath))
                File.Delete(m_filePath);
            await File.WriteAllTextAsync(m_filePath, json, CancellationToken.None);
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "Failed to write hedge records to file.");
        }
    }

    public async Task<int> ClosedHedgesCountAsync(TimeSpan maxAge, CancellationToken cancel = default)
    {
        var utcNow = DateTime.UtcNow;
        var records = await GetRecordsAsync(cancel);
        var filteredRecords = records
            .Where(r => utcNow - r.Closed < maxAge)
            .ToArray();
        return filteredRecords.Length;
    }

    private async Task<HedgeRecord[]> GetRecordsAsync(CancellationToken cancel = default)
    {
        try
        {
            if (m_records != null)
                return m_records;

            if (!File.Exists(m_filePath))
            {
                return [];
            }

            // json deserialization
            var json = await File.ReadAllTextAsync(m_filePath, cancel);
            var records = JsonSerializer.Deserialize<HedgeRecord[]>(json) ?? [];

            return records;
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "Failed to read hedge records from file.");
            return [];
        }
    }
}