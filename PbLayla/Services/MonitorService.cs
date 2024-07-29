using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.Processing;

namespace PbLayla.Services;

public class MonitorService : BackgroundService
{
    private readonly IRiskMonitor[] m_riskMonitors;
    private readonly IOptions<MonitorServiceOptions> m_options;
    private readonly ILogger<MonitorService> m_logger;

    public MonitorService(IEnumerable<IRiskMonitor> riskMonitors, 
        IOptions<MonitorServiceOptions> options, 
        ILogger<MonitorService> logger)
    {
        m_options = options;
        m_logger = logger;
        m_riskMonitors = riskMonitors.ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteRiskMonitorsAsync(stoppingToken);
            }
            catch (Exception e)
            {
                m_logger.LogWarning(e, "Error executing risk monitors");
            }
            await Task.Delay(m_options.Value.ExecutionInterval, stoppingToken);
        }
    }

    private async Task ExecuteRiskMonitorsAsync(CancellationToken stoppingToken)
    {
        var tasks = m_riskMonitors.Select(rm => rm.ExecuteAsync(stoppingToken));
        await Task.WhenAll(tasks);
    }
}
