using System.Text;
using Microsoft.Extensions.Logging;

namespace PbLayla.Model.PbConfig;

public class SymbolConfig : Dictionary<string, string>
{
    public SymbolOptions[] ParseSymbols()
    {
        var symbolOptionsList = new List<SymbolOptions>();
        foreach (var symbol in Keys)
        {
            var flags = this[symbol];
            var symbolOptions = SymbolOptions.FromFlags(symbol, flags);
            symbolOptionsList.Add(symbolOptions);
        }
        return symbolOptionsList.ToArray();
    }

    public void UpdateSymbols(SymbolOptions[] symbolOptions)
    {
        Clear();
        foreach (var symbolOption in symbolOptions)
        {
            string flags = symbolOption.ToFlags();
            Add(symbolOption.Symbol, flags);
        }
    }

    public SymbolConfig Clone()
    {
        var clone = new SymbolConfig();
        foreach (var key in Keys)
        {
            clone.Add(key, this[key]);
        }
        return clone;
    }
}