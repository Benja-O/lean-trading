using System;
using System.Collections.Generic;

namespace Trading.Domain.Exceptions
{
    /// <summary>
    /// Excepción de dominio que indica que se intentaron construir parámetros de riesgo inválidos.
    /// Se lanza exclusivamente desde RiskParameters.Create cuando una o más invariantes son violadas.
    /// 
    /// Categoría: violación de política de riesgo. NO es un ArgumentException porque la causa raíz
    /// pertenece al dominio (parámetros institucionales fuera de límites), no al contrato de programación.
    /// </summary>
    public sealed class InvalidRiskParametersException : Exception
    {
        public IReadOnlyList<string> Violations { get; }

        public InvalidRiskParametersException(IReadOnlyList<string> violations)
            : base(BuildMessage(violations))
        {
            Violations = violations;
        }

        private static string BuildMessage(IReadOnlyList<string> violations)
        {
            if (violations == null || violations.Count == 0)
            {
                return "Parámetros de riesgo inválidos (sin detalle).";
            }

            string detail = string.Join(Environment.NewLine + "  - ", violations);
            return $"Parámetros de riesgo inválidos ({violations.Count} violación/es):" +
                   Environment.NewLine + "  - " + detail;
        }
    }
}
