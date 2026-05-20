namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Niveles de severidad de log del dominio. Espejo de los cinco métodos de ITradingLogger.
    /// NO referenciar Microsoft.Extensions.Logging.LogLevel (rompería la regla de cero
    /// dependencias externas en Domain).
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}
