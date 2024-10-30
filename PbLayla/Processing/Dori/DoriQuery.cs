using PbLayla.Model.PbConfig;

namespace PbLayla.Processing.Dori;

public class DoriQuery
{
    public required string StrategyName { get; set; }
    public required IPbMultiConfig TemplateConfig { get; set; }
    public double InitialQtyPercent { get; set; }
    public double WalletBalance { get; set; }
    public bool CopyTrading { get; set; }
}