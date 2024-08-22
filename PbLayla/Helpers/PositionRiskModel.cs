using PbLayla.Model;
using PbLayla.Model.PbConfig;

namespace PbLayla.Helpers;

public class PositionRiskModel
{
    public PositionRiskModel(Position position, double positionExposure, double? positionExposureRatio, double priceActionDistance, Position? hedgePosition)
    {
        Position = position;
        PositionExposure = positionExposure;
        PositionExposureRatio = positionExposureRatio;
        PriceActionDistance = priceActionDistance;
        HedgePosition = hedgePosition;
    }

    public Position Position { get; set; }
    public double PositionExposure { get; set; }
    public double? PositionExposureRatio { get; set; }
    public double PriceActionDistance { get; set; }
    public Position? HedgePosition { get; set; }

    public bool IsOverExposed(double overExposeFilterFactor)
    {
        return PositionExposureRatio > overExposeFilterFactor;
    }

    /// <summary>
    /// Gets whether the position is stuck. Position is stuck if it is overexposed and has been stuck for a certain amount of time or price action is too far away from position price.
    /// </summary>
    /// <returns></returns>
    public bool IsStuck(double positionStuckExposureRatio, TimeSpan minStuckTime, bool isOverStageOneTotalStuckExposure, double priceDistanceStuck)
    {
        if (PositionExposureRatio == null)
            return false;
        if (PositionExposureRatio > 1.0)
            return true; // If position is overexposed, it is stuck.

        var isOverExpectedPositionExposureRatio = PositionExposureRatio > positionStuckExposureRatio;
        var stuckTime = DateTime.UtcNow - Position.UpdateTime;
        var isMinTimeStuck = stuckTime > minStuckTime;
        var canBeAssumedStuck = isOverStageOneTotalStuckExposure || isMinTimeStuck;
        var isPriceDistanceTooFar = PriceActionDistance > priceDistanceStuck && Position.UnrealizedPnl < 0;
        bool isStuck = (isOverExpectedPositionExposureRatio && canBeAssumedStuck && Position.UnrealizedPnl < 0) || isPriceDistanceTooFar;
        return isStuck;
    }
}