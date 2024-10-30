using PbLayla.Model;
using PbLayla.Model.PbConfig;
using TradeMode = PbLayla.Model.PbConfig.TradeMode;

namespace PbLayla.Helpers;

public class RiskModel
{
    public RiskModel(Balance balance, bool isOverStageOneTotalStuckExposure, IPbMultiConfig? configTemplate)
    {
        Balance = balance;
        IsOverStageOneTotalStuckExposure = isOverStageOneTotalStuckExposure;
        ConfigTemplate = configTemplate;
        LongPositions = new Dictionary<string, PositionRiskModel>();
        NakedShorts = new Dictionary<string, Position>();
    }

    public Dictionary<string, PositionRiskModel> LongPositions { get; }
    public Dictionary<string, Position> NakedShorts { get; }
    public IPbMultiConfig? ConfigTemplate { get; }
    public Balance Balance { get; }
    public bool IsOverStageOneTotalStuckExposure { get; }

    public PositionRiskModel[] FilterStuckPositions(double positionStuckExposureRatio, TimeSpan minStuckTime, double priceDistanceStuck, double overExposeFilterFactor)
    {
        if( ConfigTemplate == null)
            return [];
        var configSymbols = ConfigTemplate.ParseSymbols();
        var maintainedSymbols = configSymbols
            .Where(x => x.LongMode == TradeMode.Normal || x.LongMode == TradeMode.GracefulStop)
            .Select(x => x.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stuckPositions = LongPositions.Values
            .Where(x => maintainedSymbols.Contains(x.Position.Symbol) 
                        && x.IsStuck(positionStuckExposureRatio, minStuckTime, IsOverStageOneTotalStuckExposure, priceDistanceStuck, overExposeFilterFactor))
            .ToArray();
        return stuckPositions;
    }

    public PositionRiskModel[] FilterOverExposedPositions(double positionStuckExposureRatio, TimeSpan minStuckTime, double priceDistanceStuck, double overExposeFilterFactor)
    {
        var stuckPositions = FilterStuckPositions(positionStuckExposureRatio, minStuckTime, priceDistanceStuck, overExposeFilterFactor);
        var overExposedPositions = new List<PositionRiskModel>();
        foreach (var position in stuckPositions)
        {
            if (position.PositionExposureRatio > overExposeFilterFactor)
                overExposedPositions.Add(position);
        }

        return overExposedPositions.ToArray();
    }
}