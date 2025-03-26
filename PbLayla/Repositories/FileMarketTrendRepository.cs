using System.Text.Json;
using Microsoft.Extensions.Options;
using PbLayla.Model.Dori;

namespace PbLayla.Repositories;

public class FileMarketTrendRepository : IMarketTrendRepository
{
    private readonly ILogger<FileMarketTrendRepository> m_logger;
    private readonly string m_filePath;
    private MarketTrend? m_marketTrend;
    private string? m_marketTrendSerialized;

    public FileMarketTrendRepository(IOptions<FileMarketTrendRepositoryOptions> options, ILogger<FileMarketTrendRepository> logger)
    {
        m_logger = logger;
        string fileName = "dori_market_trend.json";
        m_filePath = Path.Combine(options.Value.FileDirectory, fileName);
        if (!Directory.Exists(options.Value.FileDirectory))
            Directory.CreateDirectory(options.Value.FileDirectory);
    }

    public async Task<MarketTrend?> TryLoadMarketTrendAsync(CancellationToken cancel = default)
    {
        try
        {
            if (m_marketTrend != null)
                return m_marketTrend;
            MoveTempFile();

            if (!File.Exists(m_filePath))
                return null;
            var json = await File.ReadAllTextAsync(m_filePath, cancel);
            var marketTrend = JsonSerializer.Deserialize<MarketTrend>(json);
            m_marketTrendSerialized = json;
            m_marketTrend = marketTrend;

            return marketTrend;
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "Failed to load market trend");
            return null;
        }
    }

    public async Task SaveMarketTrendAsync(MarketTrend marketTrend, CancellationToken cancel = default)
    {
        var json = JsonSerializer.Serialize(marketTrend,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });
        if (string.Equals(json, m_marketTrendSerialized))
            return;
        await AtomicSaveFileAsync(json, cancel);
        m_marketTrendSerialized = json;
        m_marketTrend = marketTrend;
    }

    private async Task AtomicSaveFileAsync(string json, CancellationToken cancel = default)
    {
        var tempFilePath = GetTempFile();
        await File.WriteAllTextAsync(tempFilePath, json, cancel);
        MoveTempFile(tempFilePath);
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
}