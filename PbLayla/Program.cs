using Bybit.Net.Clients;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Options;
using PbLayla.Configuration;
using PbLayla.Exchanges;
using PbLayla.HealthChecks;
using PbLayla.Helpers;
using PbLayla.PbLifeCycle;
using PbLayla.Processing;
using PbLayla.Processing.Dori;
using PbLayla.Repositories;
using PbLayla.Services;

namespace PbLayla;

internal class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddEnvironmentVariables("PBLAYLA_");
        var configuration = builder.Configuration.GetSection("PbLayla").Get<PbLaylaConfig>();
        if (configuration == null)
            throw new InvalidOperationException("PbLayla configuration is missing");
        var hc = builder.Services.AddHealthChecks();
        hc.AddCheck<BackgroundExecutionHealthCheck>("RiskMonitor");
        builder.Services.AddHostedService<MonitorService>();
        builder.Services.AddOptions<BackgroundExecutionLastStateProviderOptions>().Configure(x =>
        {
            x.RiskMonitorExecutionInterval = configuration.RiskMonitor.ExecutionInterval;
            x.AllowedExecutionDelay = configuration.RiskMonitor.AllowedExecutionDelay;
        });
        builder.Services.AddSingleton<IBackgroundExecutionLastStateProvider, BackgroundExecutionLastStateProvider>();
        builder.Services.AddOptions<MonitorServiceOptions>().Configure(x =>
        {
            x.ExecutionInterval = configuration.RiskMonitor.ExecutionInterval;
        });
        builder.Services.AddOptions<DockerPbLifeCycleControllerOptions>().Configure(x =>
        {
            x.DockerHost = configuration.Docker.DockerHost;
            x.Image = configuration.Docker.Image;
            x.ConfigsPath = configuration.Docker.ConfigsPath;
            x.ApiKeysPath = configuration.Docker.ApiKeysPath;
            x.MountConfigsPath = configuration.Docker.MountConfigsPath;
            x.MountApiKeysPath = configuration.Docker.MountApiKeysPath;
        });
        builder.Services.AddSingleton<IPbLifeCycleController, DockerPbLifeCycleController>();
        builder.Services.AddSingleton<IEnumerable<IRiskMonitor>>(sp =>
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
        builder.Services.AddSingleton<IEnumerable<ITransferProfit>>(sp =>
        {
            List<ITransferProfit> transferProfits = new List<ITransferProfit>();
            foreach (var account in configuration.Accounts)
            {
                if (string.IsNullOrWhiteSpace(account.ApiKey) || string.IsNullOrWhiteSpace(account.ApiSecret))
                    continue;
                if (!account.EnableProfitTransfer)
                    continue;
                var transferProfit = CreateBybitTransferProfit(account, sp);
                transferProfits.Add(transferProfit);
            }

            return transferProfits;
        });
        builder.Services.AddSingleton<IDoriService, DoriService>();
        builder.Services.AddOptions<DoriServiceOptions>().Configure(x =>
        {
            x.Username = configuration.Dori.Username;
            x.Password = configuration.Dori.Password;
            x.Url = configuration.Dori.Url;
        });
        bool useDori = configuration.Accounts.Any(x => x.ManageDori);
        bool marketTrendAdaptive = configuration.Accounts.Any(x => x.MarketTrendAdaptive);
        if (useDori || marketTrendAdaptive)
        {
            builder.Services.AddHostedService<DoriBackgroundService>();
            builder.Services.AddOptions<DoriBackgroundServiceOptions>().Configure(x =>
            {
                x.ExecutionInterval = configuration.Dori.ExecutionInterval;
                x.ExecutionFailInterval = configuration.Dori.ExecutionFailInterval;
                x.MarketTrendAdaptive = marketTrendAdaptive;
                x.Strategies = configuration.Accounts
                    .Where(a => a.ManageDori)
                    .Select(a => a.Name)
                    .ToArray();
            });
        }

        bool useTransferProfit = configuration.Accounts.Any(x => x.EnableProfitTransfer);
        if (useTransferProfit)
        {
            builder.Services.AddHostedService<TransferProfitService>();
            builder.Services.AddOptions<TransferProfitServiceOptions>().Configure(x =>
            {
                x.ExecutionInterval = configuration.TransferProfit.ExecutionInterval;
            });
        }

        builder.Services.AddLogging(options =>
        {
            options.AddSimpleConsole(o =>
            {
                o.UseUtcTimestamp = true;
                o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            });
        });

        var host = builder.Build();
        host.MapHealthChecks("/healthz");
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
        var pbFuturesRestClient = CreateBybitRestClient(account, services);
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
            MaxUnstuckSymbols = account.MaxUnstuckSymbols,
            ManageHedges = account.ManageHedges,
            ManagePbLifecycle = account.ManagePbLifecycle,
            ManageDori = account.ManageDori,
            InitialQtyPercent = account.InitialQtyPercent,
            DoriConfig = account.DoriConfig,
            CopyTrading = account.CopyTrading,
            ManualHedgeSymbols = account.ManualHedgeSymbols,
            PbVersion = account.PbVersion,
            FastReducePbStuckThreshold = account.FastReducePbStuckThreshold,
            CautiousUnstuckConfig = account.CautiousUnstuckConfig,
            CautiousDoriConfig = account.CautiousDoriConfig,
            CautiousDistanceStuck = account.CautiousDistanceStuck,
            CautiousDistanceCloseHedge = account.CautiousDistanceCloseHedge,
            CautiousDistanceUnstuckStuck = account.CautiousDistanceUnstuckStuck,
            CautiousDistanceUnstuckCloseHedge = account.CautiousDistanceUnstuckCloseHedge,
            NormalPbStuckThreshold = account.NormalPbStuckThreshold,
            FastReducePbLossAllowance = account.FastReducePbLossAllowance,
            NormalPbLossAllowance = account.NormalPbLossAllowance,
            MarketTrendAdaptive = account.MarketTrendAdaptive
        });
        var fileHedgeRecordRepositoryOptions = new FileHedgeRecordRepositoryOptions
        {
            AccountName = account.Name,
            FileDirectory = account.ConfigsPath,
            MaxHistory = TimeSpan.FromDays(14)
        };
        var doriService = services.GetRequiredService<IDoriService>();
        var fileHedgeRecordRepository = new FileHedgeRecordRepository(
            Options.Create(fileHedgeRecordRepositoryOptions), 
            lf.CreateLogger<FileHedgeRecordRepository>());
        var accountDataProvider = new RiskMonitor(
            monitorOptions, 
            pbFuturesRestClient,
            pbLifeCycleController,
            fileHedgeRecordRepository,
            lf.CreateLogger<RiskMonitor>(),
            doriService);
        return accountDataProvider;
    }

    private static TransferProfit CreateBybitTransferProfit(Account account, IServiceProvider services)
    {
        var transferProfitRepositoryOptions = new FileProfitTransferRepositoryOptions
        {
            AccountName = account.Name,
            FileDirectory = account.ConfigsPath,
            MaxTransactionLogsHistory = account.TransferProfitLogHistory,
        };
        var transferProfitRepository = new FileProfitTransferRepository(
            Options.Create(transferProfitRepositoryOptions),
            services.GetRequiredService<ILogger<FileProfitTransferRepository>>());
        var transferProfitOptions = new TransferProfitOptions
        {
            AccountName = account.Name,
            TransferProfitTo = account.TransferProfitTo,
            TransferProfitFrom = account.TransferProfitFrom,
            TransferProfitRatio = account.TransferProfitRatio,
            MaxLookBack = account.TransferProfitLookBack,
        };
        var pbFuturesRestClient = CreateBybitRestClient(account, services);
        var logger = services.GetRequiredService<ILogger<TransferProfit>>();
        var transferProfit = new TransferProfit(
            Options.Create(transferProfitOptions),
            pbFuturesRestClient,
            transferProfitRepository,
            logger);
        return transferProfit;
    }

    private static IPbFuturesRestClient CreateBybitRestClient(Account account, IServiceProvider services)
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
        return pbFuturesRestClient;
    }
}
