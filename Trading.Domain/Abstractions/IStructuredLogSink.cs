using System;
using System.Collections.Generic;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato para sinks de logs estructurados. Implementaciones (ej. JsonlFileLogSink)
    /// viven fuera del dominio. Esta interfaz expresa solo el contrato.
    /// </summary>
    public interface IStructuredLogSink
    {
        void Write(
            LogLevel level,
            string messageTemplate,
            IReadOnlyList<KeyValuePair<string, object?>> properties,
            Exception? exception);
    }
}
