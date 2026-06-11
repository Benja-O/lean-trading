namespace Trading.Analytics.Parsing;

/// <summary>
/// Representa un fill individual del transaction-log.csv de Lean.
/// Formato de línea: DateTime,Symbol,Buy|Sell,Quantity,Price
/// </summary>
public sealed record TradeFill(
    DateTime Timestamp,
    string Symbol,
    bool IsBuy,
    decimal Quantity,
    decimal Price);
