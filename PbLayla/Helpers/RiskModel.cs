using PbLayla.Model;
using PbLayla.Model.PbConfig;

namespace PbLayla.Helpers;

public class RiskModel
{
    public RiskModel(Balance balance, bool isOverStageOneTotalStuckExposure, PbMultiConfig configTemplate)
    {
        Balance = balance;
        IsOverStageOneTotalStuckExposure = isOverStageOneTotalStuckExposure;
        ConfigTemplate = configTemplate;
        LongPositions = new Dictionary<string, PositionRiskModel>();
        NakedShorts = new Dictionary<string, Position>();
    }

    public Dictionary<string, PositionRiskModel> LongPositions { get; }
    public Dictionary<string, Position> NakedShorts { get; }
    public PbMultiConfig ConfigTemplate { get; }
    public Balance Balance { get; }
    public bool IsOverStageOneTotalStuckExposure { get; }

    public PositionRiskModel[] FilterStuckPositions(double positionStuckExposureRatio, TimeSpan minStuckTime, double priceDistanceStuck)
    {
        var stuckPositions = LongPositions.Values
            .Where(x => x.IsStuck(positionStuckExposureRatio, minStuckTime, IsOverStageOneTotalStuckExposure, priceDistanceStuck))
            .ToArray();
        return stuckPositions;
    }

    public PositionRiskModel[] FilterOverExposedPositions(double positionStuckExposureRatio, TimeSpan minStuckTime, double priceDistanceStuck, double overExposeFilterFactor)
    {
        var stuckPositions = FilterStuckPositions(positionStuckExposureRatio, minStuckTime, priceDistanceStuck);
        var overExposedPositions = new List<PositionRiskModel>();
        foreach (var position in stuckPositions)
        {
            if (position.PositionExposureRatio > overExposeFilterFactor)
                overExposedPositions.Add(position);
        }

        return overExposedPositions.ToArray();
    }
}