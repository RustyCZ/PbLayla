using PbLayla.Model.Dori;

namespace PbLayla.Repositories;

public interface IMarketTrendRepository
{
    public Task<MarketTrend?> TryLoadMarketTrendAsync(CancellationToken cancel = default);
    public Task SaveMarketTrendAsync(MarketTrend marketTrend, CancellationToken cancel = default);
}