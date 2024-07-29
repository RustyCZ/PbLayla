using PbLayla.Model;

namespace PbLayla.Repositories;

public class HedgeRecord
{
    public Position? ClosedHedgePosition { get; set; }

    public DateTime Closed { get; set; }
}