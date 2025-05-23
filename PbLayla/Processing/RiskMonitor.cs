﻿using System.Diagnostics;
using System.Text.Json;
using Hjson;
using Microsoft.Extensions.Options;
using PbLayla.Configuration;
using PbLayla.Exchanges;
using PbLayla.Helpers;
using PbLayla.Model;
using PbLayla.Model.Dori;
using PbLayla.Model.PbConfig;
using PbLayla.PbLifeCycle;
using PbLayla.Processing.Dori;
using PbLayla.Repositories;

namespace PbLayla.Processing;

public class RiskMonitor : IRiskMonitor
{
    protected readonly record struct HedgeDistances(double PriceDistanceStuck, double PriceDistanceCloseHedge, double PriceDistanceUnstuckStuck, double PriceDistanceUnstuckCloseHedge);
    private readonly IPbFuturesRestClient m_client; 
    private readonly IOptions<RiskMonitorOptions> m_options;
    private readonly ILogger<RiskMonitor> m_logger;
    private readonly IHedgeRecordRepository m_hedgeRecordRepository;
    private readonly IDoriService m_doriService;
    private IPbMultiConfig? m_configTemplate;
    private Stopwatch? m_lastStateChangeCheck;
    private Stopwatch? m_lastDoriStateChangeCheck;
    private readonly IPbLifeCycleController m_lifeCycleController;
    private string? m_currentConfig;
    private readonly HashSet<string> m_currentUnstuckingSymbols;
    private readonly HashSet<string> m_manualHedgeSymbols;
    private AccountState m_currentAccountState;

    public RiskMonitor(IOptions<RiskMonitorOptions> options,
        IPbFuturesRestClient client,
        IPbLifeCycleController lifeCycleController,
        IHedgeRecordRepository hedgeRecordRepository,
        ILogger<RiskMonitor> logger, IDoriService doriService)
    {
        m_client = client;
        m_options = options;
        m_logger = logger;
        m_doriService = doriService;
        m_hedgeRecordRepository = hedgeRecordRepository;
        m_lifeCycleController = lifeCycleController;
        m_manualHedgeSymbols = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var valueManualHedgeSymbol in m_options.Value.ManualHedgeSymbols)
            m_manualHedgeSymbols.Add(valueManualHedgeSymbol);
        bool validOptions = ValidateOptions();
        if (!validOptions)
            throw new ArgumentException("Invalid options");
        m_logger.LogInformation("{AccountName}: Risk monitor options: {Options}",
            m_options.Value.AccountName,
            JsonSerializer.Serialize(m_options.Value, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        m_currentUnstuckingSymbols = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
    }

    private HedgeDistances CurrentHedgeDistances
    {
        get
        {
            var accountState = m_currentAccountState;
            switch (accountState)
            {
                case AccountState.AdaptiveCautious:
                case AccountState.AdaptiveCautiousStuck:
                case AccountState.AdaptiveCautiousFastReduce:
                    return new HedgeDistances(
                        m_options.Value.CautiousDistanceStuck,
                        m_options.Value.CautiousDistanceCloseHedge,
                        m_options.Value.CautiousDistanceUnstuckStuck,
                        m_options.Value.CautiousDistanceUnstuckCloseHedge);
                default:
                    return new HedgeDistances(
                        m_options.Value.PriceDistanceStuck,
                        m_options.Value.PriceDistanceCloseHedge,
                        m_options.Value.PriceDistanceUnstuckStuck,
                        m_options.Value.PriceDistanceUnstuckCloseHedge);
            }
        }
    }

    public async Task ExecuteAsync(CancellationToken cancel = default)
    {
        m_logger.LogInformation("{AccountName}: Risk monitor cycle executed.",
            m_options.Value.AccountName);
        try
        {
            IPbMultiConfig? configTemplate = null;
            if (m_options.Value.ManagePbLifecycle)
            {
                configTemplate = LoadConfigTemplate();
                if (configTemplate == null)
                {
                    m_logger.LogWarning("{AccountName}: Config template is not available", m_options.Value.AccountName);
                    return;
                }

                if (configTemplate.GetSymbolCount() == 0)
                {
                    m_logger.LogWarning("{AccountName}: Config template has no symbols", m_options.Value.AccountName);
                    return;
                }
            }

            var tickers = await m_client.GetTickersAsync(cancel);
            var balance = await m_client.GetBalancesAsync(cancel);
            var positions = await m_client.GetPositionsAsync(cancel);
            
            if (!balance.UnrealizedPnl.HasValue)
            {
                m_logger.LogInformation("{AccountName}: UnrealizedPnl is not available", m_options.Value.AccountName);
                return;
            }

            if (!balance.WalletBalance.HasValue)
            {
                m_logger.LogInformation("{AccountName}: WalletBalance is not available", m_options.Value.AccountName);
                return;
            }

            var riskModel = RiskMonitorHelpers.CalculateRiskModel(positions, tickers, balance, configTemplate, m_options.Value.StageOneTotalStuckExposure, m_logger);
            string positionSymbols = string.Empty;
            if(riskModel.LongPositions.Count > 0)
                positionSymbols = string.Join(',', riskModel.LongPositions.Values.Select(p => p.Position.Symbol));
            m_logger.LogInformation("{AccountName}: Maintaining risk for existing long positions: {Positions}", m_options.Value.AccountName, positionSymbols);

            if (configTemplate != null && m_options.Value.ManageDori && riskModel.Balance.WalletBalance.HasValue)
            {
                bool changedDori = await ManageDoriStrategyAsync(cancel, configTemplate, riskModel);
                if (changedDori)
                {
                    configTemplate = LoadConfigTemplate();
                    riskModel = RiskMonitorHelpers.CalculateRiskModel(positions, tickers, balance, configTemplate, m_options.Value.StageOneTotalStuckExposure, m_logger);
                }
            }
            
            await MaintainRiskAsync(riskModel, cancel);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "{AccountName}: Error executing risk monitor", m_options.Value.AccountName);
        }
    }

    private async Task<bool> ManageDoriStrategyAsyncInner(CancellationToken cancel, IPbMultiConfig configTemplate,
        RiskModel riskModel)
    {
        if (!riskModel.Balance.WalletBalance.HasValue)
        {
            m_logger.LogWarning("{AccountName}: Wallet balance is not available", m_options.Value.AccountName);
            return false;
        }
        var doriQuery = new DoriQuery
        {
            StrategyName = m_options.Value.AccountName,
            TemplateConfig = configTemplate.Clone(),
            InitialQtyPercent = m_options.Value.InitialQtyPercent,
            WalletBalance = (double)riskModel.Balance.WalletBalance.Value,
            CopyTrading = m_options.Value.CopyTrading
        };
        await m_doriService.UpdateDoriQueryAsync(doriQuery, cancel);

        m_lastDoriStateChangeCheck ??= Stopwatch.StartNew();
        if (m_lastDoriStateChangeCheck.Elapsed > m_options.Value.StateChangeCheckTime)
        {
            m_lastDoriStateChangeCheck.Restart();
            var doriStrategy = await m_doriService.TryGetDoriStrategyAsync(m_options.Value.AccountName, cancel);
            if (doriStrategy == null)
                m_logger.LogWarning("{AccountName}: Dori strategy is not ready", m_options.Value.AccountName);
            else
            {
                DateTime lastUpdate = doriStrategy.SymbolData
                    .OrderBy(x => x.BackTestResult.EndDate)
                    .Select(x => x.BackTestResult.EndDate)
                    .FirstOrDefault();
                double expectedTotalAdg = doriStrategy.TotalLongAdg;
                m_logger.LogInformation("{AccountName}: Dori strategy is ready, last update {lastUpdate}, expected total adg {expectedTotalAdg}.", 
                    m_options.Value.AccountName, lastUpdate, expectedTotalAdg);
                string doriTemplate = configTemplate.UpdateDoriTemplateConfig(
                    riskModel, 
                    doriStrategy, 
                    m_options.Value.AccountName,
                    m_options.Value.UnstuckConfig,
                    m_options.Value.DoriConfig,
                    m_logger);
                m_logger.LogInformation("{AccountName}: Dori template updated", m_options.Value.AccountName);
                string previousTemplate = configTemplate.SerializeConfig();
                if (string.Equals(previousTemplate, doriTemplate, StringComparison.Ordinal))
                {
                    m_logger.LogInformation("{AccountName}: Dori template is the same", m_options.Value.AccountName);
                    return false;
                }
                await SaveDoriTemplateAsync(doriTemplate, cancel);
                m_logger.LogInformation("{AccountName}: Dori template saved", m_options.Value.AccountName);
                return true;
            }
        }

        return false;
    }

    private async Task<bool> ManageDoriStrategyAsync(CancellationToken cancel, IPbMultiConfig configTemplate, RiskModel riskModel)
    {
        try
        {
            return await ManageDoriStrategyAsyncInner(cancel, configTemplate, riskModel);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "{AccountName}: Error managing Dori strategy", m_options.Value.AccountName);
        }
        return false;
    }

    private async Task MaintainRiskAsync(RiskModel riskModel, CancellationToken cancel)
    {
        if (m_options.Value.ManagePbLifecycle)
        {
            if (m_options.Value.MarketTrendAdaptive)
            {
                var accountState = await EvaluateAdaptiveAccountStateAsync(riskModel, cancel);
                m_currentAccountState = accountState;
                m_logger.LogInformation("{AccountName}: Account state is {AccountState}",
                    m_options.Value.AccountName,
                    accountState);
                switch (accountState)
                {
                    case AccountState.AdaptiveNormal:
                    case AccountState.Normal:
                        await EnsureAdaptiveNormalStateAsync(riskModel, cancel);
                        break;
                    case AccountState.AdaptiveNormalStuck:
                        await EnsureAdaptiveNormalStuckStateAsync(riskModel, cancel);
                        break;
                    case AccountState.AdaptiveCautious:
                        await EnsureAdaptiveCautiousStateAsync(riskModel, cancel);
                        break;
                    case AccountState.AdaptiveCautiousStuck:
                        await EnsureAdaptiveCautiousStuckStateAsync(riskModel, cancel);
                        break;
                    case AccountState.AdaptiveCautiousFastReduce:
                        await EnsureAdaptiveCautiousFastReduceStateAsync(riskModel, cancel);
                        break;
                    default:
                        m_logger.LogWarning("{AccountName}: Unknown adaptive account state", m_options.Value.AccountName);
                        break;
                }
            }
            else
            {
                var accountState = EvaluateAccountState(riskModel);
                m_currentAccountState = accountState;
                m_logger.LogInformation("{AccountName}: Account state is {AccountState}",
                    m_options.Value.AccountName,
                    accountState);
                switch (accountState)
                {
                    case AccountState.Normal:
                        await EnsureNormalStateAsync(riskModel, cancel);
                        break;
                    case AccountState.StageOneStuck:
                        await EnsureStageOneStuckStateAsync(riskModel, cancel);
                        break;
                    default:
                        m_logger.LogWarning("{AccountName}: Unknown account state", m_options.Value.AccountName);
                        break;
                }
            }
        }

        if (m_options.Value.ManageHedges)
        {
            await CloseNakedShorts(riskModel, cancel);
            await HedgePositionsAsync(riskModel, cancel);
            m_logger.LogInformation("{AccountName}: managed hedges", m_options.Value.AccountName);
        }
    }

    private async Task HedgePositionsAsync(RiskModel riskModel, CancellationToken cancel)
    {
        var currentHedgeDistances = CurrentHedgeDistances;
        foreach (var longPosition in riskModel.LongPositions.Values)
        {
            if (m_manualHedgeSymbols.Contains(longPosition.Position.Symbol))
                continue;
            if (m_currentUnstuckingSymbols.Contains(longPosition.Position.Symbol))
            {
                await HedgePositionAsync(
                    longPosition,
                    currentHedgeDistances.PriceDistanceUnstuckStuck,
                    currentHedgeDistances.PriceDistanceUnstuckCloseHedge, 
                    cancel);
            }
            else
            {
                await HedgePositionAsync(
                    longPosition,
                    currentHedgeDistances.PriceDistanceStuck,
                    currentHedgeDistances.PriceDistanceCloseHedge, 
                    cancel);
            }
        }
    }

    private async Task HedgePositionAsync(PositionRiskModel longPosition, double hedgeDistance, double hedgeCloseDistance, CancellationToken cancel)
    {
        bool needsHedge = longPosition.PriceActionDistance > hedgeDistance && longPosition.Position.UnrealizedPnl < 0;
        if (needsHedge)
        {
            if (longPosition.HedgePosition != null)
            {
                await MaintainExistingHedgeAsync(longPosition, cancel);
            }
            else
            {
                m_logger.LogInformation("{AccountName}: Hedging position for symbol {Symbol}", m_options.Value.AccountName, longPosition.Position.Symbol);
                var hedged = await m_client.PlaceMarketSellHedgeOrderAsync(longPosition.Position.Symbol, longPosition.Position.Quantity, cancel);
                if (!hedged)
                    m_logger.LogWarning("{AccountName}: Failed to hedge position for symbol {Symbol}", m_options.Value.AccountName, longPosition.Position.Symbol);
                else
                    m_logger.LogInformation("{AccountName}: Position for symbol {Symbol} hedged", m_options.Value.AccountName, longPosition.Position.Symbol);
            }
        }
        else
        {
            if (longPosition.HedgePosition != null)
            {
                // cancel hedge if price got back to normal and we still have attempts to cancel
                var closedHedgesCount = await m_hedgeRecordRepository.ClosedHedgesCountAsync(m_options.Value.MaxHedgeReleaseAttemptsPeriod, cancel);
                var canRelease = closedHedgesCount < m_options.Value.MaxHedgeReleaseAttempts;
                var cancelHedge = (longPosition.PriceActionDistance < hedgeCloseDistance || longPosition.Position.UnrealizedPnl > 0) 
                                  && canRelease;
                if (cancelHedge)
                {
                    m_logger.LogInformation("{AccountName}: Closing hedge for symbol {Symbol}", m_options.Value.AccountName, longPosition.Position.Symbol);
                    bool closed = await m_client.ClosePositionAsync(longPosition.HedgePosition, cancel);
                    if (!closed)
                        m_logger.LogWarning("{AccountName}: Failed to close hedge for symbol {Symbol}", m_options.Value.AccountName, longPosition.Position.Symbol);
                    else
                    {
                        m_logger.LogInformation("{AccountName}: Hedge for symbol {Symbol} closed", m_options.Value.AccountName, longPosition.Position.Symbol);
                        await m_hedgeRecordRepository.AddClosedHedgePositionAsync(longPosition.HedgePosition, cancel);
                    }
                }
                else
                {
                    // maintain hedge, in theory position can even go to profit when we exhausted releases
                    // it will be closed in naked shorts logic or when we get more attempts
                    // for the moment just maintain the hedge
                    await MaintainExistingHedgeAsync(longPosition, cancel);
                }
            }
        }
    }

    private async Task MaintainExistingHedgeAsync(PositionRiskModel longPosition, CancellationToken cancel)
    {
        if (longPosition.HedgePosition == null)
            return;
        if (longPosition.Position.Quantity < longPosition.HedgePosition.Quantity)
        {
            // close part of the hedge
            var qtyToClose = longPosition.HedgePosition.Quantity - longPosition.Position.Quantity;
            m_logger.LogInformation("{AccountName}: Reducing hedge for symbol {Symbol}", m_options.Value.AccountName, longPosition.Position.Symbol);
            var reduced = await m_client.ReduceSellHedgeAsync(longPosition.Position.Symbol, qtyToClose, cancel);
            if (!reduced)
                m_logger.LogWarning("{AccountName}: Failed to reduce hedge for symbol {Symbol}", m_options.Value.AccountName, longPosition.Position.Symbol);
            else
                m_logger.LogInformation("{AccountName}: Hedge for symbol {Symbol} reduced", m_options.Value.AccountName, longPosition.Position.Symbol);
        }
        else if (longPosition.Position.Quantity > longPosition.HedgePosition.Quantity)
        {
            // increase hedge
            var missingQty = longPosition.Position.Quantity - longPosition.HedgePosition.Quantity;
            m_logger.LogInformation("{AccountName}: Increasing hedge for symbol {Symbol}", m_options.Value.AccountName, longPosition.Position.Symbol);
            var hedged = await m_client.PlaceMarketSellHedgeOrderAsync(longPosition.Position.Symbol, missingQty, cancel);
            if (!hedged)
                m_logger.LogWarning("{AccountName}: Failed to increase hedge for symbol {Symbol}", m_options.Value.AccountName, longPosition.Position.Symbol);
            else
                m_logger.LogInformation("{AccountName}: Hedge for symbol {Symbol} increased", m_options.Value.AccountName, longPosition.Position.Symbol);
        }
    }

    private async Task CloseNakedShorts(RiskModel riskModel, CancellationToken cancel)
    {
        foreach (var riskModelNakedShort in riskModel.NakedShorts)
        {
            m_logger.LogInformation("{AccountName}: Closing naked short for symbol {Symbol}", m_options.Value.AccountName, riskModelNakedShort.Key);
            bool closed = await m_client.ClosePositionAsync(riskModelNakedShort.Value, cancel);
            if(!closed)
                m_logger.LogWarning("{AccountName}: Failed to close naked short for symbol {Symbol}", m_options.Value.AccountName, riskModelNakedShort.Key);
            else
            {
                m_logger.LogInformation("{AccountName}: Naked short for symbol {Symbol} closed", m_options.Value.AccountName, riskModelNakedShort.Key);
                await m_hedgeRecordRepository.AddClosedHedgePositionAsync(riskModelNakedShort.Value, cancel);
            }
        }
    }

    private AccountState EvaluateAccountState(RiskModel riskModel)
    {
        var stuckPositions = riskModel.FilterStuckPositions(
            m_options.Value.StuckExposureRatio,
            m_options.Value.MinStuckTime, 
            m_options.Value.PriceDistanceStuck,
            m_options.Value.OverExposeFilterFactor);
        
        if (stuckPositions.Length > 0)
            return AccountState.StageOneStuck;

        return AccountState.Normal;
    }

    private async Task<AccountState> EvaluateAdaptiveAccountStateAsync(RiskModel riskModel, CancellationToken cancel)
    {
        var marketTrend = await m_doriService.TryGetMarketTrendAsync(cancel);
        if (marketTrend == null || marketTrend.GlobalTrend == Trend.Unknown)
        {
            m_logger.LogInformation("{AccountName}: Market trend not ready, let's use unknown state", m_options.Value.AccountName);
            return AccountState.Unknown;
        }

        if (marketTrend.GlobalTrend == Trend.Bullish)
        {
            var normalStuckPositionsCount = riskModel.FilterStuckPositions(
                m_options.Value.StuckExposureRatio,
                m_options.Value.MinStuckTime,
                m_options.Value.PriceDistanceStuck,
                m_options.Value.OverExposeFilterFactor);
            var unstuckingHedgeCount = riskModel.LongPositions
                .Where(x => m_currentUnstuckingSymbols.Contains(x.Key)).Select(x => x.Value)
                .Count(x => x.PriceActionDistance > m_options.Value.PriceDistanceUnstuckStuck && x.Position.UnrealizedPnl < 0);
            if (normalStuckPositionsCount.Length == 0 && unstuckingHedgeCount == 0)
            {
                m_logger.LogInformation("{AccountName}: Market trend is bullish, no stuck positions, no unstucking hedges, let's run normal state", m_options.Value.AccountName);
                return AccountState.AdaptiveNormal;
            }

            if (normalStuckPositionsCount.Length <= m_options.Value.MaxUnstuckSymbols && unstuckingHedgeCount == 0)
            {
                m_logger.LogInformation("{AccountName}: Market trend is bullish, acceptable stuck positions count, no unstucking hedges, let's run normal state", m_options.Value.AccountName);
                return AccountState.AdaptiveNormalStuck;
            }

            if (normalStuckPositionsCount.Length > m_options.Value.MaxUnstuckSymbols)
            {
                // we have too many stuck positions, we should reduce them as soon as possible
                m_logger.LogInformation("{AccountName}: Market trend is bullish, too many stuck positions for normal, let's use cautious state with fast reduce", m_options.Value.AccountName);
                return AccountState.AdaptiveCautiousFastReduce;
            }

            if (unstuckingHedgeCount > 0)
            {
                // we have unstucking hedges, we should get rid of them as soon as possible
                m_logger.LogInformation("{AccountName}: Market trend is bullish, account has potential unstucking hedges in normal, let's use cautious state with fast reduce", m_options.Value.AccountName);
                return AccountState.AdaptiveCautiousFastReduce;
            }
        }

        if (marketTrend.GlobalTrend == Trend.Bearish)
        {
            // in bearish trend we want to use larger grid and far away hedge distances and wait it out for bullish trend to reduce positions
            var cautiousStuckPositionsCount = riskModel.FilterStuckPositions(
                m_options.Value.StuckExposureRatio,
                m_options.Value.MinStuckTime,
                m_options.Value.CautiousDistanceCloseHedge,
                m_options.Value.OverExposeFilterFactor);
            var cautiousUnstuckingHedgeCount = riskModel.LongPositions
                .Where(x => m_currentUnstuckingSymbols.Contains(x.Key)).Select(x => x.Value)
                .Count(x => x.PriceActionDistance > m_options.Value.CautiousDistanceUnstuckStuck && x.Position.UnrealizedPnl < 0);

            if (cautiousStuckPositionsCount.Length == 0 && cautiousUnstuckingHedgeCount == 0)
            {
                m_logger.LogInformation("{AccountName}: Market trend is bearish, no stuck positions, no unstucking hedges, let's run cautious state", m_options.Value.AccountName);
                return AccountState.AdaptiveCautious;
            }

            if (cautiousStuckPositionsCount.Length <= m_options.Value.MaxUnstuckSymbols && cautiousUnstuckingHedgeCount == 0)
            {
                m_logger.LogInformation("{AccountName}: Market trend is bearish, acceptable stuck positions count, no unstucking hedges, let's run cautious state", m_options.Value.AccountName);
                return AccountState.AdaptiveCautiousStuck;
            }

            if (cautiousStuckPositionsCount.Length > m_options.Value.MaxUnstuckSymbols)
            {
                // dangerous bearish scenario with some hedges
                m_logger.LogInformation("{AccountName}: Market trend is bearish, too many stuck positions for cautious, let's use cautious state with fast reduce", m_options.Value.AccountName);
                return AccountState.AdaptiveCautiousFastReduce;
            }

            if (cautiousUnstuckingHedgeCount > 0)
            {
                // very dangerous bearish scenario that has potentially bigger allocation in ver bad positions
                m_logger.LogInformation("{AccountName}: Market trend is bearish, account has potential unstucking hedges in cautious, let's use cautious state with fast reduce", m_options.Value.AccountName);
                return AccountState.AdaptiveCautiousFastReduce;
            }
        }

        m_logger.LogInformation("{AccountName}: Account state is unknown.", m_options.Value.AccountName);
        return AccountState.Unknown;
    }

    private IPbMultiConfig? LoadConfigTemplate()
    {
        try
        {
            if(m_configTemplate != null)
                return m_configTemplate;
            if (!Directory.Exists(m_options.Value.ConfigsPath))
            {
                m_logger.LogWarning("{AccountName}: Configs path does not exist", m_options.Value.AccountName);
                return null;
            }
            if (m_options.Value.ManageDori)
                ApplyDoriTemplate();
            string configTemplatePath = Path.Combine(m_options.Value.ConfigsPath, m_options.Value.ConfigTemplateFileName);
            if (!File.Exists(configTemplatePath))
            {
                m_logger.LogWarning("{AccountName}: Config template file does not exist", m_options.Value.AccountName);
                return null;
            }

            var jsonString = HjsonValue.Load(configTemplatePath).ToString();
            IPbMultiConfig? config;
            switch (m_options.Value.PbVersion)
            {
                case PbVersion.V610:
                    config = JsonSerializer.Deserialize<PbMultiConfig>(jsonString);
                    break;
                case PbVersion.V614:
                    config = JsonSerializer.Deserialize<PbMultiConfigV614>(jsonString);
                    break;
                default: 
                    throw new NotSupportedException($"PB version {m_options.Value.PbVersion} is not supported");
            }
            m_configTemplate = config;
            return config;
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "{AccountName}: Error loading config template", m_options.Value.AccountName);
            return null;
        }
    }

    private async Task SaveDoriTemplateAsync(string doriTemplate, CancellationToken cancel)
    {
        string configTemplatePath = Path.Combine(m_options.Value.ConfigsPath, m_options.Value.ConfigTemplateFileName);
        configTemplatePath += ".dori";
        if (File.Exists(configTemplatePath))
            File.Delete(configTemplatePath);
        await File.WriteAllTextAsync(configTemplatePath, doriTemplate, cancel);
        ApplyDoriTemplate();
    }

    private void ApplyDoriTemplate()
    {
        string configTemplatePath = Path.Combine(m_options.Value.ConfigsPath, m_options.Value.ConfigTemplateFileName);
        string doriConfigTemplatePath = configTemplatePath + ".dori";
        if (!File.Exists(doriConfigTemplatePath))
            return;
        if (File.Exists(configTemplatePath))
            File.Delete(configTemplatePath);
        File.Move(doriConfigTemplatePath, configTemplatePath);
        m_configTemplate = null; // should be reloaded
    }

    private async Task EnsureNormalStateAsync(RiskModel riskModel, CancellationToken cancel)
    {
        await EnsureStateAsync(
            riskModel, 
            AccountState.Normal, 
            template => template.GenerateNormalConfig(),
            StartExchangeOperationsAsync,
            cancel);
        m_currentUnstuckingSymbols.Clear();
    }

    private async Task EnsureStageOneStuckStateAsync(RiskModel riskModel, CancellationToken cancel)
    {
        await EnsureUnstuckStateAsync(riskModel,
            AccountState.StageOneStuck,
            m_options.Value.PriceDistanceUnstuckStuck,
            config => config.GenerateUnstuckConfig(m_currentUnstuckingSymbols,
                m_options.Value.UnstuckConfig,
                m_options.Value.UnstuckExposure,
                m_options.Value.DisableOthersWhileUnstucking),
            cancel);
    }

    private async Task EnsureUnstuckStateAsync(RiskModel riskModel, 
        AccountState accountState,
        double priceDistanceUnstuckStuck,
        Func<IPbMultiConfig, string> configTransformationFunc,
        CancellationToken cancel)
    {
        // first prefer previously stucked symbols
        var stuckPositions = riskModel.FilterStuckPositions(
            m_options.Value.StuckExposureRatio,
            m_options.Value.MinStuckTime,
            priceDistanceUnstuckStuck,
            m_options.Value.OverExposeFilterFactor);

        // remove symbols that are not stuck anymore
        var symbolsNotStuckAnymore = m_currentUnstuckingSymbols
            .Except(stuckPositions.Select(x => x.Position.Symbol), StringComparer.InvariantCultureIgnoreCase)
            .ToList();
        foreach (var symbolNotStuckAnymore in symbolsNotStuckAnymore)
            m_currentUnstuckingSymbols.Remove(symbolNotStuckAnymore);

        // add symbols that are overexposed
        var overexposedPositions = riskModel.FilterOverExposedPositions(
            m_options.Value.StuckExposureRatio,
            m_options.Value.MinStuckTime,
            m_options.Value.PriceDistanceStuck,
            m_options.Value.OverExposeFilterFactor);
        int maxUnstuckSymbols = m_options.Value.MaxUnstuckSymbols;
        if (m_currentUnstuckingSymbols.Count < maxUnstuckSymbols && overexposedPositions.Length > 0)
        {
            var orderedByHighestExposure = overexposedPositions.OrderByDescending(x => x.PositionExposure);
            foreach (var overexposedPosition in orderedByHighestExposure)
            {
                if (m_currentUnstuckingSymbols.Count >= maxUnstuckSymbols)
                    break;
                m_currentUnstuckingSymbols.Add(overexposedPosition.Position.Symbol);
            }
        }

        // if no preferable positions found, try to unstuck the position with the highest unrealized PnL,
        // they have the best chance to get unstuck
        if (m_currentUnstuckingSymbols.Count < maxUnstuckSymbols)
        {
            var orderedByHighestUnrealizedPnl = stuckPositions.OrderByDescending(x => x.Position.UnrealizedPnl);
            foreach (var stuckPosition in orderedByHighestUnrealizedPnl)
            {
                if (m_currentUnstuckingSymbols.Count >= maxUnstuckSymbols)
                    break;
                m_currentUnstuckingSymbols.Add(stuckPosition.Position.Symbol);
            }
        }

        await EnsureStateAsync(
            riskModel,
            accountState,
            configTransformationFunc,
            StartExchangeOperationsAsync,
            cancel);
    }

    private async Task EnsureAdaptiveCautiousFastReduceStateAsync(RiskModel riskModel, CancellationToken cancel)
    {
        // there are probably some stuck positions for normal config or already over exposed account
        string normalConfig = m_options.Value.CautiousDoriConfig;
        string unstuckConfig = m_options.Value.CautiousUnstuckConfig;
        double pbStuckThreshold = m_options.Value.FastReducePbStuckThreshold;
        double pbLossAllowance = m_options.Value.FastReducePbLossAllowance;
        m_currentUnstuckingSymbols.Clear();

        // keep unstucking only coins that are already overexposed and considered stuck under normal config
        var overexposedPositions = riskModel.FilterOverExposedPositions(
            m_options.Value.StuckExposureRatio,
            m_options.Value.MinStuckTime,
            m_options.Value.PriceDistanceStuck,
            m_options.Value.OverExposeFilterFactor);
        int maxUnstuckSymbols = m_options.Value.MaxUnstuckSymbols;
        if (m_currentUnstuckingSymbols.Count < maxUnstuckSymbols && overexposedPositions.Length > 0)
        {
            var orderedByHighestExposure = overexposedPositions.OrderByDescending(x => x.PositionExposure);
            foreach (var overexposedPosition in orderedByHighestExposure)
            {
                if (m_currentUnstuckingSymbols.Count >= maxUnstuckSymbols)
                    break;
                m_currentUnstuckingSymbols.Add(overexposedPosition.Position.Symbol);
            }
        }

        if (m_currentUnstuckingSymbols.Any())
        {
            await EnsureStateAsync(
                riskModel,
                AccountState.AdaptiveCautiousFastReduce,
                config => config.GenerateUnstuckAdaptiveTrendConfig(m_currentUnstuckingSymbols, 
                    normalConfig,
                    unstuckConfig,
                    pbStuckThreshold,
                    pbLossAllowance,
                    m_options.Value.UnstuckExposure,
                    m_options.Value.DisableOthersWhileUnstucking),
                StartExchangeOperationsAsync,
                cancel);
        }
        else
        {
            await EnsureStateAsync(
                riskModel,
                AccountState.AdaptiveCautiousFastReduce,
                config => config.GenerateNormalAdaptiveTrendConfig(normalConfig,
                    unstuckConfig,
                    pbStuckThreshold,
                    pbLossAllowance),
                StartExchangeOperationsAsync,
                cancel);
        }
    }

    private async Task EnsureAdaptiveCautiousStuckStateAsync(RiskModel riskModel, CancellationToken cancel)
    {
        string normalConfig = m_options.Value.CautiousDoriConfig;
        string unstuckConfig = m_options.Value.CautiousUnstuckConfig;
        double pbStuckThreshold = m_options.Value.NormalPbStuckThreshold;
        double pbLossAllowance = m_options.Value.NormalPbLossAllowance;
        // bearish scenario with a single stuck position that can be handled with extra wallet exposure and cautious unstuck config
        await EnsureUnstuckStateAsync(riskModel,
            AccountState.AdaptiveCautiousStuck,
            m_options.Value.PriceDistanceUnstuckStuck,
            config => config.GenerateUnstuckAdaptiveTrendConfig(m_currentUnstuckingSymbols,
                normalConfig,
                unstuckConfig,
                pbStuckThreshold,
                pbLossAllowance,
                m_options.Value.UnstuckExposure,
                m_options.Value.DisableOthersWhileUnstucking),
            cancel);
    }

    private async Task EnsureAdaptiveCautiousStateAsync(RiskModel riskModel, CancellationToken cancel)
    {
        string normalConfig = m_options.Value.CautiousDoriConfig;
        string unstuckConfig = m_options.Value.CautiousUnstuckConfig;
        double pbStuckThreshold = m_options.Value.NormalPbStuckThreshold;
        double pbLossAllowance = m_options.Value.NormalPbLossAllowance;
        await EnsureStateAsync(
            riskModel,
            AccountState.AdaptiveCautious,
            template => template.GenerateNormalAdaptiveTrendConfig(normalConfig, unstuckConfig, pbStuckThreshold, pbLossAllowance),
            StartExchangeOperationsAsync,
            cancel);
        m_currentUnstuckingSymbols.Clear();
    }

    private async Task EnsureAdaptiveNormalStuckStateAsync(RiskModel riskModel, CancellationToken cancel)
    {
        string normalConfig = m_options.Value.DoriConfig;
        string unstuckConfig = m_options.Value.UnstuckConfig;
        double pbStuckThreshold = m_options.Value.NormalPbStuckThreshold;
        double pbLossAllowance = m_options.Value.NormalPbLossAllowance;
        // bullish scenario with a single stuck position that can be handled with extra wallet exposure and normal unstuck config
        await EnsureUnstuckStateAsync(riskModel,
            AccountState.AdaptiveNormalStuck,
            m_options.Value.PriceDistanceStuck,
            config => config.GenerateUnstuckAdaptiveTrendConfig(m_currentUnstuckingSymbols,
                normalConfig,
                unstuckConfig,
                pbStuckThreshold,
                pbLossAllowance,
                m_options.Value.UnstuckExposure,
                m_options.Value.DisableOthersWhileUnstucking),
            cancel);
    }

    private async Task EnsureAdaptiveNormalStateAsync(RiskModel riskModel, CancellationToken cancel)
    {
        string normalConfig = m_options.Value.DoriConfig;
        string unstuckConfig = m_options.Value.UnstuckConfig;
        double pbStuckThreshold = m_options.Value.NormalPbStuckThreshold;
        double pbLossAllowance = m_options.Value.NormalPbLossAllowance;
        await EnsureStateAsync(
            riskModel,
            AccountState.AdaptiveNormal,
            template => template.GenerateNormalAdaptiveTrendConfig(normalConfig, unstuckConfig, pbStuckThreshold, pbLossAllowance),
            StartExchangeOperationsAsync,
            cancel);
        m_currentUnstuckingSymbols.Clear();
    }

    private async Task StartExchangeOperationsAsync(CancellationToken cancel)
    {
        await CancelBuyOrderAsync(cancel);
    }

    private async Task CancelBuyOrderAsync(CancellationToken cancel)
    {
        int retry = 3;
        while (retry-- > 0)
        {
            try
            {
                var orders = await m_client.GetOrdersAsync(cancel);
                var buyOrders = orders
                    .Where(x => x.Side == OrderSide.Buy && x.PositionMode == PositionIdx.BuyHedgeMode)
                    .ToArray();
                await m_client.CancelOrdersAsync(buyOrders, cancel);
                break;
            }
            catch (Exception e)
            {
                m_logger.LogWarning(e, "{AccountName}: Error cancelling buy orders", m_options.Value.AccountName);
                await Task.Delay(1000, cancel);
                m_logger.LogInformation("{AccountName}: Retrying cancelling buy orders", m_options.Value.AccountName);
            }
        }
    }

    private async Task EnsureStateAsync(RiskModel riskModel, AccountState expectedState, Func<IPbMultiConfig, string> configTransformationFunc, Func<CancellationToken, Task> exchangeOperationsFunc, CancellationToken cancel)
    {
        // check if enough time has passed since last state change, we don't want to query and restart docker too often
        if (!IsStateChangeCheckTimeElapsed())
            return;
        var configTemplate = riskModel.ConfigTemplate;
        if (configTemplate == null)
        {
            m_logger.LogWarning("{AccountName}: Config template is not available", m_options.Value.AccountName);
            return;
        }
            
        var newConfig = configTransformationFunc(configTemplate);
        var currentConfig = m_currentConfig;
        var currentState = await m_lifeCycleController.FindStartedAccountStateAsync(m_options.Value.AccountName, cancel);

        if (currentState == expectedState && string.Equals(currentConfig, newConfig, StringComparison.Ordinal))
        {
            m_logger.LogInformation("{AccountName}: Account state is already in '{ExpectedState}' mode", m_options.Value.AccountName, expectedState);
            return;
        }

        string newConfigFileName = FormattableString.Invariant($"{m_options.Value.AccountName}_{expectedState}.hjson");
        string newConfigPath = Path.Combine(m_options.Value.ConfigsPath, newConfigFileName);
        if (File.Exists(newConfigPath))
            File.Delete(newConfigPath);
        await File.WriteAllTextAsync(newConfigPath, newConfig, cancel);
        bool stopped = await m_lifeCycleController.StopPbAsync(m_options.Value.AccountName, cancel);
        if (!stopped)
        {
            m_logger.LogWarning("{AccountName}: Failed to stop PB", m_options.Value.AccountName);
            return;
        }
        await exchangeOperationsFunc(cancel);
        bool started = await m_lifeCycleController.StartPbAsync(m_options.Value.AccountName, newConfigFileName, expectedState, cancel);
        if (!started)
            m_logger.LogWarning("{AccountName}: Failed to start PB with config '{Config}", m_options.Value.AccountName, newConfigFileName);
        m_currentConfig = newConfig;
    }

    private bool IsStateChangeCheckTimeElapsed()
    {
        if (m_lastStateChangeCheck == null)
        {
            m_lastStateChangeCheck = Stopwatch.StartNew();
            return true;
        }

        if (m_lastStateChangeCheck.Elapsed > m_options.Value.StateChangeCheckTime)
        {
            m_lastStateChangeCheck.Restart();
            return true;
        }

        return false;
    }

    private bool ValidateOptions()
    {
        var options = m_options.Value;
        if (string.IsNullOrWhiteSpace(options.AccountName))
        {
            m_logger.LogWarning("Account name is not set");
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.ConfigsPath))
        {
            m_logger.LogWarning("Configs path is not set");
            return false;
        }

        if (m_options.Value.ManagePbLifecycle)
        {
            if (string.IsNullOrWhiteSpace(options.ConfigTemplateFileName))
            {
                m_logger.LogWarning("Config template file name is not set");
                return false;
            }

            if (options.StuckExposureRatio <= 0)
            {
                m_logger.LogWarning("Stuck exposure ratio is not set");
                return false;
            }

            if (options.MinStuckTime <= TimeSpan.Zero)
            {
                m_logger.LogWarning("Min stuck time is not set");
                return false;
            }

            if (options.OverExposeFilterFactor <= 0)
            {
                m_logger.LogWarning("Over expose filter factor is not set");
                return false;
            }

            if (options.UnstuckExposure <= 0)
            {
                m_logger.LogWarning("Unstuck exposure is not set");
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.UnstuckConfig))
            {
                m_logger.LogWarning("Unstuck config is not set");
                return false;
            }

            if (options.StateChangeCheckTime <= TimeSpan.Zero)
            {
                m_logger.LogWarning("State change check time is not set");
                return false;
            }

            if (!Directory.Exists(options.ConfigsPath))
            {
                m_logger.LogWarning("Configs path does not exist");
                return false;
            }

            if (!File.Exists(Path.Combine(options.ConfigsPath, options.ConfigTemplateFileName)))
            {
                m_logger.LogWarning("Config template file does not exist {Template}", options.ConfigTemplateFileName);
                return false;
            }

            if (!File.Exists(Path.Combine(options.ConfigsPath, options.UnstuckConfig)))
            {
                m_logger.LogWarning("Unstuck config file does not exist {UnstuckConfig}", options.UnstuckConfig);
                return false;
            }

            if (options.ManageDori && string.IsNullOrEmpty(options.DoriConfig))
            {
                m_logger.LogWarning("Dori config is not set");
                return false;
            }

            if (m_options.Value.MarketTrendAdaptive)
            {
                if (string.IsNullOrEmpty(options.CautiousDoriConfig))
                {
                    m_logger.LogWarning("Cautious Dori config is not set");
                    return false;
                }
            }
        }

        return true;
    }
}