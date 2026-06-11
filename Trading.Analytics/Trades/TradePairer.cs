using Trading.Analytics.Parsing;

namespace Trading.Analytics.Trades;

/// <summary>
/// Empareja fills Buy+Sell del transaction-log en CompletedTrade.
/// La estrategia es long-only, así que cada Buy se empareja con el
/// siguiente Sell del mismo símbolo (FIFO por símbolo).
/// Fills sin par (posiciones abiertas al cierre del período) se descartan.
/// </summary>
public static class TradePairer
{
    public static List<CompletedTrade> Pair(List<TradeFill> fills)
    {
        var pendingBuys = new Dictionary<string, Queue<TradeFill>>(StringComparer.Ordinal);
        var completedTrades = new List<CompletedTrade>();

        foreach (var fill in fills.OrderBy(f => f.Timestamp))
        {
            if (fill.IsBuy)
            {
                if (!pendingBuys.TryGetValue(fill.Symbol, out var queue))
                {
                    queue = new Queue<TradeFill>();
                    pendingBuys[fill.Symbol] = queue;
                }
                queue.Enqueue(fill);
            }
            else
            {
                if (pendingBuys.TryGetValue(fill.Symbol, out var queue) && queue.Count > 0)
                {
                    var buyFill = queue.Dequeue();
                    var pnl = (fill.Price - buyFill.Price) * buyFill.Quantity;
                    completedTrades.Add(new CompletedTrade(
                        EntryTime: buyFill.Timestamp,
                        ExitTime: fill.Timestamp,
                        Symbol: fill.Symbol,
                        EntryPrice: buyFill.Price,
                        ExitPrice: fill.Price,
                        Quantity: buyFill.Quantity,
                        PnlUsdt: pnl));
                }
                // Sell sin Buy previo = arrastre del período anterior → descartar.
            }
        }

        return completedTrades.OrderBy(t => t.ExitTime).ToList();
    }
}
