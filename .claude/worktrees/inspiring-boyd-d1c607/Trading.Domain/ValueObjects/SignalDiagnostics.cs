using System;
using System.Collections.Generic;

namespace Trading.Domain.ValueObjects
{
    /// <summary>
    /// Diagnóstico de una señal: snapshot de los valores que la estrategia usó al decidir.
    ///
    /// Cada estrategia llena las claves correspondientes a sus indicadores. Las claves son strings
    /// libres (no enum) porque varían por estrategia. Convención sugerida: nombres CamelCase
    /// equivalentes a la propiedad del indicador (ej. "EmaFast", "EmaSlow", "PreviousSignal", "RsiValue").
    ///
    /// El SignalAuditor compara estos valores contra su propio recálculo independiente.
    ///
    /// Inmutable. Si no hay valores (estrategia que no expone diagnostics todavía), usar SignalDiagnostics.Empty.
    /// </summary>
    public sealed class SignalDiagnostics
    {
        public IReadOnlyDictionary<string, decimal> Values { get; }

        public SignalDiagnostics(IReadOnlyDictionary<string, decimal> values)
        {
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public static SignalDiagnostics Empty { get; } =
            new SignalDiagnostics(new Dictionary<string, decimal>());

        public bool IsEmpty => Values.Count == 0;
    }
}
