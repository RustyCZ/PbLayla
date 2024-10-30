using System.Text.Json.Serialization;

namespace PbLayla.Model.PbConfig;

public class UniversalLiveConfigSide
{
    [JsonPropertyName("ddown_factor")]
    public double DdownFactor { get; set; } = 0.8697;

    [JsonPropertyName("ema_span_0")]
    public double EmaSpan0 { get; set; } = 776.7;

    [JsonPropertyName("ema_span_1")]
    public double EmaSpan1 { get; set; } = 774.3;

    [JsonPropertyName("initial_eprice_ema_dist")]
    public double InitialEpriceEmaDist { get; set; } = -0.008465;

    [JsonPropertyName("initial_qty_pct")]
    public double InitialQtyPct { get; set; } = 0.01167;

    [JsonPropertyName("markup_range")]
    public double MarkupRange { get; set; } = 0.002187;

    [JsonPropertyName("min_markup")]
    public double MinMarkup { get; set; } = 0.008534;

    [JsonPropertyName("n_close_orders")]
    public double NCloseOrders { get; set; } = 4.0;

    [JsonPropertyName("rentry_pprice_dist")]
    public double RentryPpriceDist { get; set; } = 0.04938;

    [JsonPropertyName("rentry_pprice_dist_wallet_exposure_weighting")]
    public double RentryPpriceDistWalletExposureWeighting { get; set; } = 2.143;

    public UniversalLiveConfigSide Clone()
    {
        return new UniversalLiveConfigSide
        {
            DdownFactor = DdownFactor,
            EmaSpan0 = EmaSpan0,
            EmaSpan1 = EmaSpan1,
            InitialEpriceEmaDist = InitialEpriceEmaDist,
            InitialQtyPct = InitialQtyPct,
            MarkupRange = MarkupRange,
            MinMarkup = MinMarkup,
            NCloseOrders = NCloseOrders,
            RentryPpriceDist = RentryPpriceDist,
            RentryPpriceDistWalletExposureWeighting = RentryPpriceDistWalletExposureWeighting
        };
    }
}