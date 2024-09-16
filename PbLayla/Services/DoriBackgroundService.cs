using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.Processing.Dori;

namespace PbLayla.Services;

public class DoriBackgroundService : BackgroundService
{
    private readonly ILogger<DoriBackgroundService> m_logger;
    private readonly IOptions<DoriBackgroundServiceOptions> m_options;
    private readonly IDoriService m_doriService;

    public DoriBackgroundService(ILogger<DoriBackgroundService> logger, 
        IOptions<DoriBackgroundServiceOptions> options, 
        IDoriService doriService)
    {
        m_logger = logger;
        m_options = options;
        m_doriService = doriService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool success = false;
            try
            {
                m_logger.LogInformation("Querying Dori Service...");
                success = await QueryDoriAsync(stoppingToken);
                m_logger.LogInformation("Querying Dori Service... Done");
            }
            catch (Exception e)
            {
                m_logger.LogWarning(e, "Error querying Dori Service");
            }
            await Task.Delay(success ? m_options.Value.ExecutionInterval : m_options.Value.ExecutionFailInterval, stoppingToken);
        }
    }

    private async Task<bool> QueryDoriAsync(CancellationToken cancel)
    {
        bool allSuccess = true;
        
        foreach (var strategy in m_options.Value.Strategies)
        {
            if (cancel.IsCancellationRequested)
                break;
            try
            {
                m_logger.LogInformation("Querying Dori for strategy {strategy}", strategy);
                bool success = await m_doriService.QueryDoriAsync(strategy, cancel);
                m_logger.LogInformation("Querying Dori for strategy {strategy} with result {success}... Done", strategy, success);
                if (!success)
                    allSuccess = false;
            }
            catch (Exception e)
            {
                m_logger.LogWarning(e, "Error querying Dori for strategy {strategy}", strategy);
                allSuccess = false;
            }
        }

        return allSuccess;
    }
}
