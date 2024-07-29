using PbLayla.Model;

namespace PbLayla.Exchanges;

public interface IPbFuturesRestClient
{
    Task<Balance> GetBalancesAsync(CancellationToken cancel = default);
    Task<Position[]> GetPositionsAsync(CancellationToken cancel = default);
    Task<bool> ClosePositionAsync(Position position, CancellationToken cancel = default);
    Task<Order[]> GetOrdersAsync(CancellationToken cancel = default);
    Task CancelOrdersAsync(Order[] orders, CancellationToken cancel = default);
    Task<Ticker[]> GetTickersAsync(CancellationToken cancel = default);
    Task<bool> PlaceMarketSellHedgeOrderAsync(string symbol, decimal quantity, CancellationToken cancel = default);
    Task<bool> ReduceSellHedgeAsync(string symbol, decimal quantity,
        CancellationToken cancel = default);
}
