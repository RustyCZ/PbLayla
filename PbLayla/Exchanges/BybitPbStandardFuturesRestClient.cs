using Bybit.Net.Enums;
using Bybit.Net.Interfaces.Clients;
using Microsoft.Extensions.Logging;
using PbLayla.Helpers;
using PbLayla.Mapping;
using Balance = PbLayla.Model.Balance;

namespace PbLayla.Exchanges;

public class BybitPbStandardFuturesRestClient : BybitPbFuturesRestClientBase
{
    private readonly IBybitRestClient m_bybitRestClient;

    public BybitPbStandardFuturesRestClient(IBybitRestClient bybitRestClient,
        ILogger<BybitPbStandardFuturesRestClient> logger)
        : base(bybitRestClient, logger)
    {
        m_bybitRestClient = bybitRestClient;
    }

    public override async Task<Balance> GetBalancesAsync(CancellationToken cancel = default)
    {
        var balance = await ExchangePolicies.RetryForever
            .ExecuteAsync(async () =>
            {
                var balanceResult = await m_bybitRestClient.V5Api.Account.GetBalancesAsync(AccountType.Contract,
                    null,
                    cancel);
                if (balanceResult.GetResultOrError(out var data, out var error))
                    return data;
                throw new InvalidOperationException(error.Message);
            });
        foreach (var b in balance.List)
        {
            if (b.AccountType == AccountType.Contract)
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
