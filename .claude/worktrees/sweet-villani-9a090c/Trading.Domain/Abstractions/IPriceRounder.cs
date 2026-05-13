using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Redondea precios al tick size del instrumento. Operación de venue, no de dominio puro,
    /// pero la abstraemos porque es necesaria al colocar órdenes (SL/TP precios) y testearla
    /// con un IInstrumentMetadata fake es trivial.
    /// 
    /// Implementación de referencia trivial (PriceRounder) vive en Application/Sizing,
    /// usa IInstrumentMetadata. Se inyecta como servicio.
    /// </summary>
    public interface IPriceRounder
    {
        decimal Round(InstrumentId instrumentId, decimal price);
    }
}
