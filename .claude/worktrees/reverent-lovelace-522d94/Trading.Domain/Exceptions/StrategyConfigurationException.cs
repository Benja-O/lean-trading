using System;
using System.Collections.Generic;

namespace Trading.Domain.Exceptions
{
    /// <summary>
    /// Excepción agregada de errores de configuración detectados durante la carga.
    /// Acumula TODOS los errores encontrados antes de fallar para evitar reinicios sucesivos
    /// arreglando un error a la vez.
    /// </summary>
    public sealed class StrategyConfigurationException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public StrategyConfigurationException(IReadOnlyList<string> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors;
        }

        private static string BuildMessage(IReadOnlyList<string> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return "Configuración de estrategias inválida (sin detalle).";
            }

            string detail = string.Join(Environment.NewLine + Environment.NewLine, errors);
            return $"Configuración de estrategias inválida. {errors.Count} error/es detectados:" +
                   Environment.NewLine + Environment.NewLine + detail;
        }
    }
}
