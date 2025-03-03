namespace PbLayla.Processing;

public enum AccountState
{
    // non adaptive states

    /// <summary>
    /// No risk state
    /// </summary>
    Unknown,
    /// <summary>
    /// Everything is normal run default PB operation
    /// </summary>
    Normal,
    /// <summary>
    /// One or more positions have already exhausted the trading grid and are stuck
    /// Position needs to be above 'StuckExposure' and more than 'MinStuckTime' time or total wallet exposure needs to be above 'StageOneTotalStuckExposure'
    /// to be considered stuck.
    /// PB will be reconfigured to ignore all positions except for the stuck one and use the wide grid with higher wallet exposure to unstuck it.
    /// Other existing positions will have close orders set at take profit distance to try to reduce the total wallet exposure.
    /// Other existing positions will not add up any more exposure at this stage.
    /// </summary>
    StageOneStuck,

    // adaptive states based on Dori trend

    /// <summary>
    /// Use normal configuration
    /// </summary>
    AdaptiveNormal,
    /// <summary>
    /// Use normal configuration with unstuck
    /// </summary>
    AdaptiveNormalStuck,
    /// <summary>
    /// Use cautious configuration with wider grid
    /// </summary>
    AdaptiveCautious,
    /// <summary>
    /// Use cautious configuration with wider grid and unstuck
    /// </summary>
    AdaptiveCautiousStuck,
    /// <summary>
    /// Use cautious configuration with wider grid and reduce position size faster
    /// There are probably some stuck positions for normal config or already over exposed account
    /// We don't want to play with any extra wallet exposure and reduce positions as fast as possible
    /// </summary>
    AdaptiveCautiousFastReduce,
}