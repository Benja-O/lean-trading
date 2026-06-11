using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Tests.Fakes
{
    /// <summary>
    /// Implementación fake de IMicrostructureProvider para tests unitarios.
    /// Permite registrar barras microestructurales individuales con Add()
    /// y consultarlas exactamente como lo haría la estrategia en producción.
    /// </summary>
    public class FakeMicrostructureProvider : IMicrostructureProvider
    {
        private readonly Dictionary<(string Ticker, DateTime BarUtc), MicrostructureBar> _data = new();

        public void Add(MicrostructureBar bar)
        {
            var key = (bar.InstrumentId.Ticker, DateTime.SpecifyKind(bar.BarUtc, DateTimeKind.Utc));
            _data[key] = bar;
        }

        public MicrostructureBar? GetBar(InstrumentId instrumentId, DateTime barUtc)
        {
            var key = (instrumentId.Ticker, DateTime.SpecifyKind(barUtc, DateTimeKind.Utc));
            _data.TryGetValue(key, out var bar);
            return bar;
        }

        public bool HasDataFor(InstrumentId instrumentId) =>
            _data.Keys.Any(k => k.Ticker == instrumentId.Ticker);
    }
}
