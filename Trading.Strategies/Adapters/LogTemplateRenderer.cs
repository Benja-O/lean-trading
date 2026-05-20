using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Helper estático para renderizar y extraer propiedades de message templates con
    /// placeholders nombrados ({OrderId}, {Price}). Extraído de LeanLogger para reutilización
    /// desde JsonlFileLogSink.
    /// </summary>
    public static class LogTemplateRenderer
    {
        // Captura {Identifier} donde Identifier es alfanumérico (sin format specifier como {Foo:N2}).
        private static readonly Regex NamedPlaceholderPattern =
            new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

        /// <summary>
        /// Renderiza el template aplicando los argumentos posicionalmente.
        /// Si los conteos no coinciden, devuelve lo que pudo renderizar sin lanzar excepción.
        /// </summary>
        public static string Render(string messageTemplate, object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
                return messageTemplate;

            int placeholderIndex = 0;
            return NamedPlaceholderPattern.Replace(
                messageTemplate,
                match =>
                {
                    int idx = placeholderIndex++;
                    return idx < arguments.Length
                        ? (arguments[idx]?.ToString() ?? string.Empty)
                        : match.Value; // sin arg disponible: conservar el placeholder original
                });
        }

        /// <summary>
        /// Extrae los nombres de los placeholders y los empareja posicionalmente con los argumentos.
        /// Si los conteos no coinciden, devuelve los pares que pudo emparejar. NUNCA lanza excepción.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, object?>> ExtractProperties(
            string messageTemplate, object[] arguments)
        {
            var result = new List<KeyValuePair<string, object?>>();

            if (arguments == null || arguments.Length == 0)
                return result;

            var matches = NamedPlaceholderPattern.Matches(messageTemplate);
            int pairCount = System.Math.Min(matches.Count, arguments.Length);

            for (int i = 0; i < pairCount; i++)
            {
                result.Add(new KeyValuePair<string, object?>(matches[i].Groups[1].Value, arguments[i]));
            }

            return result;
        }
    }
}
