using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PbLayla.HealthChecks;

public class BackgroundExecutionHealthCheck : IHealthCheck
{
    private readonly IBackgroundExecutionLastStateProvider m_lastStateProvider;

    public BackgroundExecutionHealthCheck(IBackgroundExecutionLastStateProvider lastStateProvider)
    {
        m_lastStateProvider = lastStateProvider;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.FromResult(m_lastStateProvider.HasExecutedInTime
            ? HealthCheckResult.Healthy("Risk monitor has executed in time")
            : HealthCheckResult.Unhealthy("Risk monitor has not executed in time"));
    }
}