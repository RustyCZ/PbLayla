using PbLayla.Model;

namespace PbLayla.Repositories;

public interface IHedgeRecordRepository
{
    Task AddClosedHedgePositionAsync(Position position, CancellationToken cancel = default);
    Task<int> ClosedHedgesCountAsync(TimeSpan maxAge, CancellationToken cancel = default);
}