namespace Trading.Analytics.Trades;

/// <summary>
/// Round trip completo: un Buy fill + su Sell fill correspondiente.
/// PnlUsdt = (ExitPrice - EntryPrice) × Quantity.
/// </summary>
public sealed record CompletedTrade(
    DateTime EntryTime,
    DateTime ExitTime,
    string Symbol,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal PnlUsdt)
{
    /// <summary>Retorno fraccional del trade: (exit - entry) / entry.</summary>
    public decimal ReturnFraction => EntryPrice > 0 ? (ExitPrice - EntryPrice) / EntryPrice : 0m;
}
