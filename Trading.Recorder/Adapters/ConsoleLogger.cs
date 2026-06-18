using System;
using System.Text.RegularExpressions;
using Trading.Domain.Abstractions;

namespace Trading.Recorder.Adapters
{
    internal sealed class ConsoleLogger : ITradingLogger
    {
        private static string Render(string template, object[] args)
        {
            int i = 0;
            return Regex.Replace(template, @"\{[^}]+\}", _ =>
                i < args.Length ? args[i++]?.ToString() ?? "(null)" : "?");
        }

        public void Debug(string messageTemplate, params object[] arguments) =>
            Console.WriteLine($"[DBG] {Render(messageTemplate, arguments)}");

        public void Info(string messageTemplate, params object[] arguments) =>
            Console.WriteLine($"[INF] {Render(messageTemplate, arguments)}");

        public void Warning(string messageTemplate, params object[] arguments) =>
            Console.WriteLine($"[WRN] {Render(messageTemplate, arguments)}");

        public void Error(string messageTemplate, params object[] arguments) =>
            Console.WriteLine($"[ERR] {Render(messageTemplate, arguments)}");

        public void Critical(string messageTemplate, params object[] arguments) =>
            Console.WriteLine($"[CRT] {Render(messageTemplate, arguments)}");
    }
}
