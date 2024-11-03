using Microsoft.Extensions.Options;

namespace PbLayla.HealthChecks;

public class BackgroundExecutionLastStateProvider : IBackgroundExecutionLastStateProvider
{
    private readonly object m_syncRoot = new object();
    private readonly IOptions<BackgroundExecutionLastStateProviderOptions> m_options;
    private DateTime m_lastExecutionTime;
    
    public BackgroundExecutionLastStateProvider(IOptions<BackgroundExecutionLastStateProviderOptions> options)
    {
        m_options = options;
    }

    public bool HasExecutedInTime
    {
        get
        {
            lock (m_syncRoot)
            {
                var inTime = (DateTime.UtcNow - m_lastExecutionTime) <= (m_options.Value.RiskMonitorExecutionInterval + m_options.Value.AllowedExecutionDelay);
                return inTime;
            }
        }
    }

    public void UpdateLastRiskMonitorExecution(DateTime executionTime)
    {
        lock (m_syncRoot)
        {
            m_lastExecutionTime = executionTime;
        }
    }
}