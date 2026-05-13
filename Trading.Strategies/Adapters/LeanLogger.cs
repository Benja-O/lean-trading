using System;
using System.Text.RegularExpressions;
using QuantConnect.Algorithm;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Adapta los métodos de log de QCAlgorithm al contrato ITradingLogger del dominio.
    ///
    /// Convierte templates con placeholders nombrados ({OrderId}, {Price}) a posicionales
    /// ({0}, {1}) antes de pasarlos a string.Format. QCAlgorithm no soporta structured
    /// logging nativo; si en el futuro se persisten eventos estructurados a un sink externo,
    /// se hará desde una capa distinta sin tocar este adaptador.
    /// </summary>
    public sealed class LeanLogger : ITradingLogger
    {
        // Captura {Identifier} donde Identifier es alfanumérico (sin format specifier
        // como {Foo:N2}, que no usamos por convención en este sistema).
        private static readonly Regex NamedPlaceholderPattern =
            new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

        private readonly QCAlgorithm _algorithm;

        public LeanLogger(QCAlgorithm algorithm)
        {
            _algorithm = algorithm;
        }

        public void Debug(string messageTemplate, params object[] arguments)
            => _algorithm.Debug(Format(messageTemplate, arguments));

        public void Info(string messageTemplate, params object[] arguments)
            => _algorithm.Log(Format(messageTemplate, arguments));

        public void Warning(string messageTemplate, params object[] arguments)
            => _algorithm.Log("WARN: " + Format(messageTemplate, arguments));

        public void Error(string messageTemplate, params object[] arguments)
            => _algorithm.Error(Format(messageTemplate, arguments));

        public void Critical(string messageTemplate, params object[] arguments)
            => _algorithm.Error("CRITICAL: " + Format(messageTemplate, arguments));

        private static string Format(string messageTemplate, object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
                return messageTemplate;

            int placeholderIndex = 0;
            string positionalTemplate = NamedPlaceholderPattern.Replace(
                messageTemplate,
                _ => "{" + placeholderIndex++ + "}");

            return string.Format(positionalTemplate, arguments);
        }
    }
}
