using Bybit.Net.Enums;
using Bybit.Net.Interfaces.Clients;
using Microsoft.Extensions.Logging;
using PbLayla.Helpers;
using PbLayla.Mapping;
using PbLayla.Model;

namespace PbLayla.Exchanges;

public class BybitPbUnifiedFuturesRestClient : BybitPbFuturesRestClientBase
{
    private readonly IBybitRestClient m_bybitRestClient;

    public BybitPbUnifiedFuturesRestClient(IBybitRestClient bybitRestClient,
        ILogger<BybitPbUnifiedFuturesRestClient> logger)
        : base(bybitRestClient, logger)
    {
        m_bybitRestClient = bybitRestClient;
    }

    public override async Task<Balance> GetBalancesAsync(CancellationToken cancel = default)
    {
        var balance = await ExchangePolicies.RetryForever
            .ExecuteAsync(async () =>
            {
                var balanceResult = await m_bybitRestClient.V5Api.Account.GetBalancesAsync(AccountType.Unified,
                    null,
                    cancel);
                if (balanceResult.GetResultOrError(out var data, out var error))
                    return data;
                throw new InvalidOperationException(error.Message);
            });
        foreach (var b in balance.List)
        {
            if (b.AccountType == AccountType.Unified)
            {
                var asset = b.Assets.FirstOrDefault(x =>
                    string.Equals(x.Asset, Assets.QuoteAsset, StringComparison.OrdinalIgnoreCase));
                if (asset != null)
                {
                    var contract = asset.ToBalance();
                    return contract;
                }
            }
        }

        return new Balance();
    }
}
