using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PbLayla.Model.Dori;
using Xunit;

namespace PbLayla.Tests.PbDori;

public class PbDoriApiTest
{
    [Fact(Skip = "Manual test")]
    public async Task QueryStrategyApiResult()
    {
        HttpClient client = new HttpClient();
        const string url = "";
        const string username = "";
        const string password = "";
        const string strategyName = "";
        const int maxSymbolCount = 15;
        const double minAllowedExchangeLeverage = 10.0;
        const double initialOrderSize = 17.0;
        const bool filterCopyTradeEnabled = false;
        string query = FormattableString.Invariant($"StrategyResults?strategyName={strategyName}&maxSymbolCount={maxSymbolCount}&minAllowedExchangeLeverage={minAllowedExchangeLeverage}&initialOrderSize={initialOrderSize}&filterCopyTradeEnabled={filterCopyTradeEnabled}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
        string responseContent = await client.GetStringAsync($"{url}{query}");
        Assert.NotNull(responseContent);
        var response = JsonSerializer.Deserialize<StrategyApiResult>(responseContent);
        Assert.NotNull(response);
    }
}
