using System;
using System.Globalization;
using System.IO;

namespace Trading.Recorder.Feed
{
    /// <summary>
    /// Persistencia del cursor <c>fromId</c> (último aggregateTradeId procesado) por símbolo.
    /// Permite que el recorder reanude exactamente donde quedó tras un reinicio, sin gaps.
    /// </summary>
    public interface IAggTradeCursorStore
    {
        /// <summary>Último aggId persistido para el símbolo, o null si no hay (cold start).</summary>
        long? Load(string symbol);

        /// <summary>Persiste el último aggId procesado para el símbolo (escritura atómica).</summary>
        void Save(string symbol, long lastAggregateTradeId);
    }

    /// <summary>
    /// Implementación en disco: un archivo por símbolo en
    /// <c>{storageDir}/cursors/{SYMBOL}_aggid.txt</c>. Escritura atómica (temp + move) para no
    /// dejar el cursor a medio escribir ante una caída del proceso.
    /// </summary>
    public sealed class FileAggTradeCursorStore : IAggTradeCursorStore
    {
        private readonly string _directory;

        public FileAggTradeCursorStore(string storageDirectory)
        {
            if (string.IsNullOrWhiteSpace(storageDirectory))
                throw new ArgumentException("storageDirectory requerido.", nameof(storageDirectory));

            _directory = Path.Combine(storageDirectory, "cursors");
            Directory.CreateDirectory(_directory);
        }

        public long? Load(string symbol)
        {
            string path = PathFor(symbol);
            if (!File.Exists(path)) return null;

            string text = File.ReadAllText(path).Trim();
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                ? value
                : null;
        }

        public void Save(string symbol, long lastAggregateTradeId)
        {
            string path          = PathFor(symbol);
            string temporaryPath = path + ".tmp";

            File.WriteAllText(temporaryPath, lastAggregateTradeId.ToString(CultureInfo.InvariantCulture));
            File.Move(temporaryPath, path, overwrite: true);
        }

        private string PathFor(string symbol) =>
            Path.Combine(_directory, $"{symbol.ToUpperInvariant()}_aggid.txt");
    }
}
