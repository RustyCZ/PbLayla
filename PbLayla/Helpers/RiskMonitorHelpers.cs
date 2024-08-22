using Microsoft.Extensions.Logging;
using PbLayla.Model;
using PbLayla.Model.PbConfig;
using PbLayla.Processing;

namespace PbLayla.Helpers;

public static class RiskMonitorHelpers
{
    public static double CalculatePositionExposure(Position position, Balance balance)
    {
        if (!balance.WalletBalance.HasValue)
            return 0;
        if (!position.AveragePrice.HasValue)
            return 0;
        var positionValue = (double)(position.AveragePrice.Value * position.Quantity);
        var positionExposure = positionValue / (double)balance.WalletBalance.Value;
        return positionExposure;
    }

    public static double CalculatePositionExposureRatio(Position position, Balance balance, double expectedMaxWalletExposure)
    {
        var positionExposure = CalculatePositionExposure(position, balance);
        var positionExposureRatio = positionExposure / expectedMaxWalletExposure;
        return positionExposureRatio;
    }

    public static double CalculateTotalExposureRatio(Position[] positions, Balance balance)
    {
        if (!balance.WalletBalance.HasValue)
            return 0;
        var totalCurrentExposure = positions
            .Where(x => x.AveragePrice.HasValue)
            .Sum(x => (double)x.AveragePrice!.Value * (double)x.Quantity);
        var totalCurrentExposureRatio = totalCurrentExposure / (double)balance.WalletBalance.Value;
        return totalCurrentExposureRatio;
    }

    public static double CalculatePriceDistance(Position position, Ticker ticker)
    {
        if (!position.AveragePrice.HasValue)
            return 0;
        double positionPrice = (double)position.AveragePrice.Value;
        if (positionPrice <= 0)
            return 0;
        double currentPrice = (double)ticker.LastPrice;
        double priceDistance = Math.Abs((currentPrice - positionPrice) / positionPrice);
        return priceDistance;
    }

    public static Position[] FilterStuckPositions(Position[] positions, 
        Balance balance, 
        PbMultiConfig configTemplate, 
        double totalStuckExposure,
        double positionStuckExposureRatio,
        TimeSpan minStuckTime)
    {
        var stuckPositions = new List<Position>();
        var expectedMaxPositionExposure = configTemplate.TweLong / configTemplate.Symbols.Count;
        var totalExposure = CalculateTotalExposureRatio(positions, balance);
        var isOverStageOneTotalStuckExposure = totalExposure > totalStuckExposure;
        foreach (var position in positions)
        {
            var positionExposureRatio = CalculatePositionExposureRatio(position, balance, expectedMaxPositionExposure);
            var isOverExpectedPositionExposureRatio = positionExposureRatio > positionStuckExposureRatio;
            var stuckTime = DateTime.UtcNow - position.CreateTime;
            var isMinTimeStuck = stuckTime > minStuckTime;
            var canBeAssumedStuck = isOverStageOneTotalStuckExposure || isMinTimeStuck;
            if (isOverExpectedPositionExposureRatio && canBeAssumedStuck)
            {
                stuckPositions.Add(position);
            }
        }

        return stuckPositions.ToArray();
    }

    public static Position[] FilterOverExposedPositions(Position[] positions, Balance balance, PbMultiConfig configTemplate, double overExposeFilterFactor)
    {
        var expectedMaxPositionExposure = configTemplate.TweLong / configTemplate.Symbols.Count;
        var overExposedPositions = new List<Position>();
        foreach (var position in positions)
        {
            var positionExposureRatio = CalculatePositionExposureRatio(position, balance, expectedMaxPositionExposure);
            if (positionExposureRatio > overExposeFilterFactor)
            {
                overExposedPositions.Add(position);
            }
        }

        return overExposedPositions.ToArray();
    }

    public static PositionExposure[] CalculatePositionExposures(Position[] positions, Balance balance)
    {
        var positionExposures = new List<PositionExposure>();
        foreach (var position in positions)
        {
            var positionExposure = CalculatePositionExposure(position, balance);
            positionExposures.Add(new PositionExposure(position, positionExposure));
        }

        return positionExposures.ToArray();
    }

    public static PositionRiskModel CalculatePositionRiskModel(Position position, 
        Ticker ticker, 
        Balance balance, 
        PbMultiConfig? configTemplate, 
        Position? hedgePosition)
    {
        var priceDistance = CalculatePriceDistance(position, ticker);
        var positionExposure = CalculatePositionExposure(position, balance);
        double? positionExposureRatio = null;
        if (configTemplate != null)
        {
            var expectedMaxPositionExposure = configTemplate.TweLong / configTemplate.Symbols.Count;
            positionExposureRatio = CalculatePositionExposureRatio(position, balance, expectedMaxPositionExposure);
        }
        
        var positionRiskModel = new PositionRiskModel(position, positionExposure, positionExposureRatio, priceDistance, hedgePosition);
        return positionRiskModel;
    }

    public static RiskModel CalculateRiskModel(Position[] positions, 
        Ticker[] tickers, 
        Balance balance, 
        PbMultiConfig? configTemplate,
        double stageOneTotalStuckExposure,
        ILogger logger)
    {
        var shortPositions = positions.Where(x => x.Side == PositionSide.Sell)
            .DistinctBy(x => x.Symbol)
            .ToDictionary(x => x.Symbol);
        var longPositions = positions.Where(x => x.Side == PositionSide.Buy)
            .DistinctBy(x => x.Symbol)
            .ToDictionary(x => x.Symbol);
        var tickerBySymbol = tickers
            .DistinctBy(x => x.Symbol)
            .ToDictionary(x => x.Symbol);
        var totalExposure = CalculateTotalExposureRatio(positions, balance);
        var isOverStageOneTotalStuckExposure = totalExposure > stageOneTotalStuckExposure;
        var riskModel = new RiskModel(balance, isOverStageOneTotalStuckExposure, configTemplate);
        foreach (var longPositionsValue in longPositions.Values)
        {
            var hedgePosition = shortPositions.GetValueOrDefault(longPositionsValue.Symbol);
            var ticker = tickerBySymbol.GetValueOrDefault(longPositionsValue.Symbol);
            if (ticker == null)
            {
                logger.LogWarning($"Ticker for symbol {longPositionsValue.Symbol} not found.");
                continue;
            }
            var positionRiskModel = CalculatePositionRiskModel(longPositionsValue, ticker, balance, configTemplate, hedgePosition);
            riskModel.LongPositions.Add(longPositionsValue.Symbol, positionRiskModel);
            shortPositions.Remove(longPositionsValue.Symbol);
        }

        foreach (var shortPosition in shortPositions.Values)
            riskModel.NakedShorts.Add(shortPosition.Symbol, shortPosition);

        return riskModel;
    }
}