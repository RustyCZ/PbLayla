using PbLayla.Processing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PbLayla.Services;

public class TransferProfitService : BackgroundService
{
    private readonly IOptions<TransferProfitServiceOptions> m_options;
    private readonly ILogger<TransferProfitService> m_logger;
    private readonly ITransferProfit[] m_transferProfits;

    public TransferProfitService(IOptions<TransferProfitServiceOptions> options,
        IEnumerable<ITransferProfit> transferProfits, 
        ILogger<TransferProfitService> logger)
    {
        m_logger = logger;
        m_options = options;
        m_transferProfits = transferProfits.ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteTransferProfitsAsync(stoppingToken);
            }
            catch (Exception e)
            {
                m_logger.LogWarning(e, "Error executing profit transfers");
            }
            await Task.Delay(m_options.Value.ExecutionInterval, stoppingToken);
        }
    }

    private async Task ExecuteTransferProfitsAsync(CancellationToken stoppingToken)
    {
        var tasks = m_transferProfits.Select(rm => rm.ExecuteAsync(stoppingToken));
        await Task.WhenAll(tasks);
    }
}
