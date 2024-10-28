namespace PbLayla.Processing;
public interface ITransferProfit
{
    Task ExecuteAsync(CancellationToken cancel = default);
}