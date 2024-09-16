using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PbLayla.Model.Dori;
using PbLayla.Model.PbConfig;

namespace PbLayla.Processing.Dori;

public class DoriService : IDoriService
{
    private readonly IOptions<DoriServiceOptions> m_options;
    private readonly ConcurrentDictionary<string, DoriQuery> m_queries;
    private readonly ConcurrentDictionary<string, StrategyApiResult> m_results;

    public DoriService(IOptions<DoriServiceOptions> options)
    {
        m_options = options;
        m_queries = new ConcurrentDictionary<string, DoriQuery>();
        m_results = new ConcurrentDictionary<string, StrategyApiResult>();
    }

    public Task UpdateDoriQueryAsync(DoriQuery query, CancellationToken cancel)
    {
        m_queries[query.StrategyName] = query;
        return Task.CompletedTask;
    }

    public async Task<bool> QueryDoriAsync(string strategyName, CancellationToken cancel = default)
    {
        if(!m_queries.TryGetValue(strategyName, out var query))
            return false;
        var symbols = query.TemplateConfig.Symbols.ParseSymbols();
        // only normal and graceful stop symbols are counted, rest is for special manual purposes in template
        int maxSymbolCount = 0;
        double minAllowedExchangeLeverage = 10.0;
        foreach (var symbol in symbols)
        {
            if (symbol.LongMode ==TradeMode.Normal || symbol.LongMode == TradeMode.GracefulStop)
                maxSymbolCount++;
            if (symbol.LeverageSetOnExchange.HasValue && symbol.LeverageSetOnExchange.Value > minAllowedExchangeLeverage)
                minAllowedExchangeLeverage = symbol.LeverageSetOnExchange.Value;
        }

        double totalExposureLong = query.TemplateConfig.TweLong;
        double exposurePerSymbol = totalExposureLong / maxSymbolCount;
        double walletBalancePerSymbol = query.WalletBalance * exposurePerSymbol;
        double initialOrderSize = walletBalancePerSymbol * query.InitialQtyPercent;
        bool filterCopyTradeEnabled = query.CopyTrading;
        string formattedQuery = FormattableString.Invariant($"StrategyResults?strategyName={strategyName}&maxSymbolCount={maxSymbolCount}&minAllowedExchangeLeverage={minAllowedExchangeLeverage}&initialOrderSize={initialOrderSize}&filterCopyTradeEnabled={filterCopyTradeEnabled}");
        using var client = new HttpClient();
        if (!string.IsNullOrEmpty(m_options.Value.Username) && !string.IsNullOrEmpty(m_options.Value.Password))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", 
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{m_options.Value.Username}:{m_options.Value.Password}")));
        }

        string url = m_options.Value.Url;
        if (string.IsNullOrEmpty(url))
            return false;
        if (!url.EndsWith('/'))
            url += '/';
        string fullUrl = FormattableString.Invariant($"{url}{formattedQuery}");
        string responseContent = await client.GetStringAsync(fullUrl, cancel);
        if (string.IsNullOrEmpty(responseContent))
            return false;
        var response = JsonSerializer.Deserialize<StrategyApiResult>(responseContent);
        if (response == null)
            return false;
        if (!response.DataAvailable)
            return false;

        m_results[strategyName] = response;
        
        return true;
    }

    public Task<StrategyApiResult?> TryGetDoriStrategyAsync(string strategyName, CancellationToken cancel = default)
    {
        if (m_results.TryGetValue(strategyName, out var result))
            return Task.FromResult<StrategyApiResult?>(result);
        return Task.FromResult<StrategyApiResult?>(null);
    }
}