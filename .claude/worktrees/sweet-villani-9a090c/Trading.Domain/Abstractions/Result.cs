using System;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Resultado de una operación que puede fallar por motivos de negocio esperados.
    ///
    /// Se distingue de las excepciones: las excepciones señalan condiciones excepcionales
    /// (invariantes rotos, fallas de infraestructura). Result&lt;T&gt; representa flujos
    /// esperados que pueden no producir un valor (precio inválido, cantidad bajo el mínimo).
    ///
    /// readonly record struct evita allocations en el hot path: cada barra produce
    /// uno o varios Result, y la frecuencia justifica el struct sobre la clase.
    ///
    /// El parámetro genérico TFailureReason permite que cada componente defina su propio
    /// enum de motivos (SizingFailureReason, RoutingFailureReason, etc.) sin acoplar
    /// tipos entre dominios.
    /// </summary>
    public readonly record struct Result<TValue, TFailureReason>
        where TFailureReason : struct, Enum
    {
        public bool IsSuccess { get; }
        public TValue Value { get; }
        public TFailureReason FailureReason { get; }
        public string FailureDescription { get; }

        private Result(bool isSuccess, TValue value, TFailureReason failureReason, string failureDescription)
        {
            IsSuccess = isSuccess;
            Value = value;
            FailureReason = failureReason;
            FailureDescription = failureDescription;
        }

        public bool IsFailure => !IsSuccess;

        /// <summary>Construye un resultado exitoso con el valor producido.</summary>
        public static Result<TValue, TFailureReason> Success(TValue value)
            => new(true, value, default, string.Empty);

        /// <summary>
        /// Construye un resultado fallido. La descripción es opcional y debe contener
        /// información de diagnóstico (valores recibidos, contexto) para logs humanos.
        /// El caller que decide acciones debe usar FailureReason (enum), NO parsear la descripción.
        /// </summary>
        public static Result<TValue, TFailureReason> Failure(TFailureReason reason, string description = "")
            => new(false, default, reason, description ?? string.Empty);
    }

    /// <summary>
    /// Variante sin valor de Result. Para operaciones que solo señalan éxito o fallo.
    /// Comparte semántica y reglas con Result&lt;T, TFailureReason&gt;.
    /// </summary>
    public readonly record struct Result<TFailureReason>
        where TFailureReason : struct, Enum
    {
        public bool IsSuccess { get; }
        public TFailureReason FailureReason { get; }
        public string FailureDescription { get; }

        private Result(bool isSuccess, TFailureReason failureReason, string failureDescription)
        {
            IsSuccess = isSuccess;
            FailureReason = failureReason;
            FailureDescription = failureDescription;
        }

        public bool IsFailure => !IsSuccess;

        public static Result<TFailureReason> Success()
            => new(true, default, string.Empty);

        public static Result<TFailureReason> Failure(TFailureReason reason, string description = "")
            => new(false, reason, description ?? string.Empty);
    }
}
