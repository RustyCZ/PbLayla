namespace PbLayla.Model.PbConfig;

public interface IPbMultiConfig
{
    double StuckThreshold { get; set; }

    double LossAllowancePct { get; set; }

    SymbolOptions[] ParseSymbols();

    int GetSymbolCount();

    IPbMultiConfig Clone();

    string SerializeConfig();

    void UpdateSymbols(SymbolOptions[] symbols);

    double GetTweLong();
}