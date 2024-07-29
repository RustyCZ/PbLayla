namespace PbLayla.Model
{
    public readonly record struct Balance(
        decimal? Equity,
        decimal? WalletBalance,
        decimal? UnrealizedPnl,
        decimal? RealizedPnl);
}
