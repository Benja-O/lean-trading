using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Provee features microestructurales (derivadas de AggTrades) para una barra 1h dada.
    ///
    /// El contrato es de solo lectura: el proveedor carga los datos al inicio y sirve lookups.
    /// Retorna null cuando no hay datos para el instrumento + timestamp consultado (barra fuera
    /// del rango histórico descargado, o símbolo sin dataset cargado).
    ///
    /// Las estrategias que lo usan deben manejar null gracefully: si no hay datos microestructurales,
    /// la señal debe degradarse a Flat o al comportamiento por defecto, nunca lanzar excepción.
    /// </summary>
    public interface IMicrostructureProvider
    {
        /// <summary>
        /// Devuelve la barra microestructural para el instrumento y timestamp dados.
        /// El timestamp debe coincidir con el inicio de la barra 1h (floor al minuto cero).
        /// Retorna null si no hay datos disponibles.
        /// </summary>
        MicrostructureBar? GetBar(InstrumentId instrumentId, DateTime barUtc);

        /// <summary>True si el proveedor tiene datos cargados para el instrumento dado.</summary>
        bool HasDataFor(InstrumentId instrumentId);
    }
}
