namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Logger del dominio con soporte para structured logging.
    ///
    /// Los mensajes se pasan como TEMPLATE con placeholders nombrados estilo Serilog/MEL
    /// (ej. "Order {OrderId} filled at {Price}") y los valores se pasan como argumentos
    /// posicionales en el mismo orden. La implementación es responsable de combinar template
    /// y argumentos para producir el mensaje final o de preservar la estructura para
    /// agregadores que la consuman (ej. Seq, Elastic).
    ///
    /// PROHIBIDO en los callers: interpolación de strings ($"...") como messageTemplate.
    /// Eso anula el propósito de structured logging y reintroduce el anti-patrón que este
    /// contrato elimina (ver AI.md sección Logging y Anti-patrones).
    /// </summary>
    public interface ITradingLogger
    {
        /// <summary>Detalle fino para diagnóstico (eventos residuales, transiciones internas).</summary>
        void Debug(string messageTemplate, params object[] arguments);

        /// <summary>Ciclo de vida normal: órdenes, cambios de estado.</summary>
        void Info(string messageTemplate, params object[] arguments);

        /// <summary>Condiciones degradadas, rechazos, retries.</summary>
        void Warning(string messageTemplate, params object[] arguments);

        /// <summary>Excepciones manejadas o errores con contexto.</summary>
        void Error(string messageTemplate, params object[] arguments);

        /// <summary>Eventos críticos: kill switch, drawdown breach, pérdida de conexión.</summary>
        void Critical(string messageTemplate, params object[] arguments);
    }
}
