namespace PbLayla.HealthChecks;

public interface IBackgroundExecutionLastStateProvider
{
    bool HasExecutedInTime { get; }

    void UpdateLastRiskMonitorExecution(DateTime executionTime);
}