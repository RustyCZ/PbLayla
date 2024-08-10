using Bybit.Net.Clients;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.Configuration;
using PbLayla.Exchanges;
using PbLayla.Helpers;
using PbLayla.PbLifeCycle;
using PbLayla.Processing;
using PbLayla.Repositories;
using PbLayla.Services;

namespace PbLayla;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = Host
            .CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables("PBLAYLA_");
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration.GetSection("PbLayla").Get<PbLaylaConfig>();
                if (configuration == null)
                    throw new InvalidOperationException("PbLayla configuration is missing");

                services.AddHostedService<MonitorService>();
                services.AddOptions<MonitorServiceOptions>().Configure(x =>
                {
                    x.ExecutionInterval = configuration.RiskMonitor.ExecutionInterval;
                });
                services.AddOptions<DockerPbLifeCycleControllerOptions>().Configure(x =>
                {
                    x.DockerHost = configuration.Docker.DockerHost;
                    x.Image = configuration.Docker.Image;
                    x.ConfigsPath = configuration.Docker.ConfigsPath;
                    x.ApiKeysPath = configuration.Docker.ApiKeysPath;
                    x.MountConfigsPath = configuration.Docker.MountConfigsPath;
                    x.MountApiKeysPath = configuration.Docker.MountApiKeysPath;
                });
                services.AddSingleton<IPbLifeCycleController, DockerPbLifeCycleController>();
                services.AddSingleton<IEnumerable<IRiskMonitor>>(sp =>
                {
                    List<IRiskMonitor> accountDataProviders = new List<IRiskMonitor>();
                    foreach (var account in configuration.Accounts)
                    {
                        if (string.IsNullOrWhiteSpace(account.ApiKey) || string.IsNullOrWhiteSpace(account.ApiSecret))
                            continue;
                        var accountDataProvider = CreateRiskMonitor(account, sp);
                        accountDataProviders.Add(accountDataProvider);
                    }

                    return accountDataProviders;
                });
                services.AddLogging(options =>
                {
                    options.AddSimpleConsole(o =>
                    {
                        o.UseUtcTimestamp = true;
                        o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                    });
                });
            })
            .Build();
        var lf = host.Services.GetRequiredService<ILoggerFactory>();
        ApplicationLogging.LoggerFactory = lf;
        await host.RunAsync();
    }

    private static RiskMonitor CreateRiskMonitor(Account account, IServiceProvider services)
    {
        switch (account.Exchange)
        {
            case Exchange.Bybit:
                return CreateBybitRiskMonitor(account, services);
            case Exchange.Binance:
                throw new NotSupportedException();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static RiskMonitor CreateBybitRiskMonitor(Account account, IServiceProvider services)
    {
        BybitRestClient client = new BybitRestClient(options =>
        {
            options.RateLimitingBehaviour = RateLimitingBehaviour.Wait;
            options.V5Options.ApiCredentials = new ApiCredentials(account.ApiKey, account.ApiSecret);
            options.ReceiveWindow = TimeSpan.FromSeconds(10);
            options.AutoTimestamp = true;
            options.TimestampRecalculationInterval = TimeSpan.FromSeconds(10);
        });
        
        IPbFuturesRestClient pbFuturesRestClient;
        if (account.IsUnified)
        {
            var logger = services.GetRequiredService<ILogger<BybitPbUnifiedFuturesRestClient>>();
            pbFuturesRestClient = new BybitPbUnifiedFuturesRestClient(client, logger);
        }
        else
        {
            var logger = services.GetRequiredService<ILogger<BybitPbStandardFuturesRestClient>>();
            pbFuturesRestClient = new BybitPbStandardFuturesRestClient(client, logger);
        }
        var lf = services.GetRequiredService<ILoggerFactory>();
        var pbLifeCycleController = services.GetRequiredService<IPbLifeCycleController>();
        var monitorOptions = Options.Create(new RiskMonitorOptions()
        {
            AccountName = account.Name,
            ConfigsPath = account.ConfigsPath,
            DisableOthersWhileUnstucking = account.DisableOthersWhileUnstucking,
            StuckExposureRatio = account.StuckExposureRatio,
            OverExposeFilterFactor = account.OverExposeFilterFactor,
            UnstuckConfig = account.UnstuckConfig,
            UnstuckExposure = account.UnstuckExposure,
            ConfigTemplateFileName = account.ConfigTemplateFileName,
            MinStuckTime = account.MinStuckTime,
            StageOneTotalStuckExposure = account.StageOneTotalStuckExposure,
            StateChangeCheckTime = account.StateChangeCheckTime,
            PriceDistanceStuck = account.PriceDistanceStuck,
            PriceDistanceCloseHedge = account.PriceDistanceCloseHedge,
            PriceDistanceUnstuckStuck = account.PriceDistanceUnstuckStuck,
            PriceDistanceUnstuckCloseHedge = account.PriceDistanceUnstuckCloseHedge,
            MaxHedgeReleaseAttempts = account.MaxHedgeReleaseAttempts,
            MaxHedgeReleaseAttemptsPeriod = account.MaxHedgeReleaseAttemptsPeriod,
            MaxUnstuckSymbols = account.MaxUnstuckSymbols
        });
        var fileHedgeRecordRepositoryOptions = new FileHedgeRecordRepositoryOptions
        {
            AccountName = account.Name,
            FileDirectory = account.ConfigsPath,
            MaxHistory = TimeSpan.FromDays(14)
        };
        var fileHedgeRecordRepository = new FileHedgeRecordRepository(
            Options.Create(fileHedgeRecordRepositoryOptions), 
            lf.CreateLogger<FileHedgeRecordRepository>());
        var accountDataProvider = new RiskMonitor(
            monitorOptions, 
            pbFuturesRestClient,
            pbLifeCycleController,
            fileHedgeRecordRepository,
            lf.CreateLogger<RiskMonitor>());
        return accountDataProvider;
    }
}
