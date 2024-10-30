namespace PbLayla.Model.PbConfig;

public interface IPbMultiConfig
{
    SymbolOptions[] ParseSymbols();

    int GetSymbolCount();

    IPbMultiConfig Clone();

    string SerializeConfig();

    void UpdateSymbols(SymbolOptions[] symbols);

    double GetTweLong();
}