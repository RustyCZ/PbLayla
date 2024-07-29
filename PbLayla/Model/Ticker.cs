using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PbLayla.Model;

public class Ticker
{
    public string Symbol { get; set; } = string.Empty;

    public decimal? BestAskPrice { get; set; }

    public decimal? BestBidPrice { get; set; }

    public decimal LastPrice { get; set; }

    public decimal? FundingRate { get; set; }

    public DateTime Timestamp { get; set; }
}
