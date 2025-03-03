using PbLayla.Model.Dori;

namespace PbLayla.Processing.Dori;

public interface IDoriService
{
    Task UpdateDoriQueryAsync(DoriQuery query, CancellationToken cancel = default);

    Task<bool> QueryDoriAsync(string strategyName, CancellationToken cancel = default);

    Task<bool> QueryDoriMarketTrendAsync(CancellationToken cancel = default);

    Task<StrategyApiResult?> TryGetDoriStrategyAsync(string strategyName, CancellationToken cancel = default);

    Task<MarketTrend?> TryGetMarketTrendAsync(CancellationToken cancel = default);
}