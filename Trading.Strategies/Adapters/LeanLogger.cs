using QuantConnect.Algorithm;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Adapta los métodos de log de QCAlgorithm al contrato ITradingLogger del dominio.
    ///
    /// Convierte templates con placeholders nombrados ({OrderId}, {Price}) a posicionales
    /// ({0}, {1}) antes de pasarlos a string.Format. La lógica de parseo vive en
    /// LogTemplateRenderer. Adicionalmente, delega cada evento al IStructuredLogSink
    /// inyectado (ej. JsonlFileLogSink) para persistencia JSONL en paralelo.
    /// </summary>
    public sealed class LeanLogger : ITradingLogger
    {
        private readonly QCAlgorithm _algorithm;
        private readonly IStructuredLogSink _structuredLogSink;

        public LeanLogger(QCAlgorithm algorithm, IStructuredLogSink structuredLogSink)
        {
            _algorithm = algorithm;
            _structuredLogSink = structuredLogSink;
        }

        public void Debug(string messageTemplate, params object[] arguments)
        {
            _structuredLogSink.Write(
                LogLevel.Debug, messageTemplate,
                LogTemplateRenderer.ExtractProperties(messageTemplate, arguments),
                exception: null);
            _algorithm.Debug(Format(messageTemplate, arguments));
        }

        public void Info(string messageTemplate, params object[] arguments)
        {
            _structuredLogSink.Write(
                LogLevel.Info, messageTemplate,
                LogTemplateRenderer.ExtractProperties(messageTemplate, arguments),
                exception: null);
            _algorithm.Log(Format(messageTemplate, arguments));
        }

        public void Warning(string messageTemplate, params object[] arguments)
        {
            _structuredLogSink.Write(
                LogLevel.Warning, messageTemplate,
                LogTemplateRenderer.ExtractProperties(messageTemplate, arguments),
                exception: null);
            _algorithm.Log("WARN: " + Format(messageTemplate, arguments));
        }

        public void Error(string messageTemplate, params object[] arguments)
        {
            _structuredLogSink.Write(
                LogLevel.Error, messageTemplate,
                LogTemplateRenderer.ExtractProperties(messageTemplate, arguments),
                exception: null);
            _algorithm.Error(Format(messageTemplate, arguments));
        }

        public void Critical(string messageTemplate, params object[] arguments)
        {
            _structuredLogSink.Write(
                LogLevel.Critical, messageTemplate,
                LogTemplateRenderer.ExtractProperties(messageTemplate, arguments),
                exception: null);
            _algorithm.Error("CRITICAL: " + Format(messageTemplate, arguments));
        }

        private static string Format(string messageTemplate, object[] arguments)
            => LogTemplateRenderer.Render(messageTemplate, arguments);
    }
}
