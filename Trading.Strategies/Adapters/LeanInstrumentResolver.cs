using System.Collections.Generic;
using QuantConnect;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Mapeo bidireccional entre InstrumentId del dominio y QuantConnect.Symbol.
    /// 
    /// Es el ÚNICO lugar del sistema que conoce ambas representaciones.
    /// Otros adaptadores Lean dependen de este resolver para hacer la conversión.
    /// 
    /// Se popula durante Initialize() del host con cada AddCryptoFuture() registrado.
    /// </summary>
    public sealed class LeanInstrumentResolver
    {
        private readonly Dictionary<string, Symbol> _tickerToSymbol = new();
        private readonly Dictionary<Symbol, InstrumentId> _symbolToInstrumentId = new();

        public void Register(Symbol symbol)
        {
            string ticker = symbol.Value;
            _tickerToSymbol[ticker] = symbol;
            _symbolToInstrumentId[symbol] = new InstrumentId(ticker);
        }

        public Symbol Resolve(InstrumentId instrumentId)
        {
            if (!_tickerToSymbol.TryGetValue(instrumentId.Ticker, out var symbol))
            {
                throw new KeyNotFoundException(
                    $"InstrumentId '{instrumentId.Ticker}' no está registrado en el resolver. " +
                    "Verificar que se haya llamado Register() durante Initialize().");
            }
            return symbol;
        }

        public InstrumentId Resolve(Symbol symbol)
        {
            if (!_symbolToInstrumentId.TryGetValue(symbol, out var instrumentId))
            {
                // Fallback: si no está registrado, lo derivamos del ticker. Esto no debería
                // ocurrir con uso normal, pero protege contra Symbols que llegan en eventos
                // antes de Register (race en escenarios futuros con descubrimiento dinámico).
                return new InstrumentId(symbol.Value);
            }
            return instrumentId;
        }
    }
}
