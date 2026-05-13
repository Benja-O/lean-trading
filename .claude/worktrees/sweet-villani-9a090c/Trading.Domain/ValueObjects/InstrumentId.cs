using System;

namespace Trading.Domain.ValueObjects
{
    /// <summary>
    /// Identificador de instrumento del dominio. Reemplaza el uso directo de QuantConnect.Symbol.
    /// 
    /// Es un value object inmutable: igualdad por valor, no por referencia. Dos InstrumentId
    /// con el mismo Ticker son iguales, lo que permite usarlos como clave en diccionarios y sets
    /// sin las complicaciones del Symbol de Lean (que incluye SecurityType, Market, etc.).
    /// 
    /// El adaptador Lean (LeanInstrumentResolver) traduce entre InstrumentId y Symbol.
    /// </summary>
    public sealed class InstrumentId : IEquatable<InstrumentId>
    {
        public string Ticker { get; }

        public InstrumentId(string ticker)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                throw new ArgumentException(
                    "InstrumentId.Ticker no puede ser nulo, vacío o solo espacios.", nameof(ticker));
            }
            Ticker = ticker;
        }

        public bool Equals(InstrumentId other) =>
            other is not null && string.Equals(Ticker, other.Ticker, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as InstrumentId);

        public override int GetHashCode() => Ticker.GetHashCode(StringComparison.Ordinal);

        public override string ToString() => Ticker;

        public static bool operator ==(InstrumentId left, InstrumentId right) =>
            left is null ? right is null : left.Equals(right);

        public static bool operator !=(InstrumentId left, InstrumentId right) => !(left == right);
    }
}
