using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Trading.Application.Microstructure;
using Trading.Application.Tests.Fakes;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Microstructure
{
    /// <summary>
    /// Tests de MicrostructureRegistry.
    ///
    /// Cobertura:
    /// - GetBar devuelve la barra correcta para instrumento + timestamp exacto.
    /// - GetBar devuelve null para timestamp no encontrado.
    /// - GetBar devuelve null para instrumento sin datos cargados.
    /// - HasDataFor devuelve true tras Load exitoso, false si el CSV no existe.
    /// - Load con CSV inexistente loguea Warning y no lanza excepción.
    /// - Load con CSV vacío (solo header) loguea Warning y HasDataFor devuelve false.
    /// - Líneas con columnas insuficientes se saltean con Warning.
    /// - Valores NaN en features se parsean sin excepción.
    /// </summary>
    public class MicrostructureRegistryTests : IDisposable
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime Bar1 = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Bar2 = new DateTime(2021, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        private readonly FakeTradingLogger _logger = new();
        private readonly MicrostructureRegistry _registry;
        private readonly string _tempDir;

        public MicrostructureRegistryTests()
        {
            _registry = new MicrostructureRegistry(_logger);
            _tempDir  = Path.Combine(Path.GetTempPath(), $"micro_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string WriteCsv(string content)
        {
            var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }

        private static string ValidCsvWith(DateTime bar1, DateTime bar2) =>
            // Columnas (índices 0-15):
            // bar,open,high,low,close,volume,buy_volume,sell_volume,trade_count,mean_trade_size,
            // ofi,buy_sell_ratio,cvd_delta,arrival_rate,price_return,cvd
            $"bar,open,high,low,close,volume,buy_volume,sell_volume,trade_count,mean_trade_size,ofi,buy_sell_ratio,cvd_delta,arrival_rate,price_return,cvd\n" +
            $"{bar1:yyyy-MM-dd HH:mm:sszzz},29000,29100,28900,29050,100,60,40,1200,0.05,0.2,1.5,20,1200,0.002,20\n" +
            $"{bar2:yyyy-MM-dd HH:mm:sszzz},29050,29200,29000,29150,120,70,50,1400,0.06,0.3,1.4,20,1400,0.003,40\n";

        // ── Tests ─────────────────────────────────────────────────────────────────

        [Fact]
        public void GetBar_RetornaBarraCorrecta_ParaTimestampExacto()
        {
            var path = WriteCsv(ValidCsvWith(Bar1, Bar2));
            _registry.Load(Btc, path);

            var result = _registry.GetBar(Btc, Bar1);

            result.Should().NotBeNull();
            result!.InstrumentId.Should().Be(Btc);
            result.BarUtc.Should().Be(Bar1);
            result.Ofi.Should().BeApproximately(0.2, 1e-10);
            result.CvdDelta.Should().BeApproximately(20, 1e-10);
            result.Cvd.Should().BeApproximately(20, 1e-10);
            result.ArrivalRate.Should().BeApproximately(1200, 1e-10);
            result.MeanTradeSize.Should().BeApproximately(0.05, 1e-10);
            result.BuySellRatio.Should().BeApproximately(1.5, 1e-10);
            result.PriceReturn.Should().BeApproximately(0.002, 1e-10);
        }

        [Fact]
        public void GetBar_RetornaNull_ParaTimestampNoEncontrado()
        {
            var path = WriteCsv(ValidCsvWith(Bar1, Bar2));
            _registry.Load(Btc, path);

            var result = _registry.GetBar(Btc, Bar1.AddHours(5));

            result.Should().BeNull();
        }

        [Fact]
        public void GetBar_RetornaNull_ParaInstrumentoSinDatos()
        {
            var result = _registry.GetBar(Btc, Bar1);

            result.Should().BeNull();
        }

        [Fact]
        public void HasDataFor_RetornaTrue_TrasLoadExitoso()
        {
            var path = WriteCsv(ValidCsvWith(Bar1, Bar2));
            _registry.Load(Btc, path);

            _registry.HasDataFor(Btc).Should().BeTrue();
        }

        [Fact]
        public void HasDataFor_RetornaFalse_SiCsvNoExiste()
        {
            _registry.Load(Btc, Path.Combine(_tempDir, "noexiste.csv"));

            _registry.HasDataFor(Btc).Should().BeFalse();
            _logger.WarningEntries.Should().Contain(e => e.MessageTemplate.Contains("no encontrado"));
        }

        [Fact]
        public void Load_NoLanzaExcepcion_SiCsvNoExiste()
        {
            var act = () => _registry.Load(Btc, Path.Combine(_tempDir, "noexiste.csv"));

            act.Should().NotThrow();
        }

        [Fact]
        public void Load_CsvVacio_HasDataForRetornaFalse()
        {
            var path = WriteCsv("bar,open,high,low,close,volume,buy_volume,sell_volume,trade_count,mean_trade_size,ofi,buy_sell_ratio,cvd_delta,arrival_rate,price_return,cvd\n");
            _registry.Load(Btc, path);

            _registry.HasDataFor(Btc).Should().BeFalse();
        }

        [Fact]
        public void Load_LineasConColumnasInsuficientes_SeSalteanConWarning()
        {
            var csv = "bar,open,high,low,close,volume,buy_volume,sell_volume,trade_count,mean_trade_size,ofi,buy_sell_ratio,cvd_delta,arrival_rate,price_return,cvd\n" +
                      "2021-01-01 00:00:00+00:00,solo,tres,columnas\n" +
                      $"{Bar2:yyyy-MM-dd HH:mm:sszzz},29050,29200,29000,29150,120,70,50,1400,0.06,0.3,1.4,20,1400,0.003,40\n";
            var path = WriteCsv(csv);
            _registry.Load(Btc, path);

            _registry.GetBar(Btc, Bar1).Should().BeNull();
            _registry.GetBar(Btc, Bar2).Should().NotBeNull();
            _logger.WarningEntries.Should().Contain(e => e.MessageTemplate.Contains("columnas insuficientes"));
        }

        [Fact]
        public void GetBar_NaN_EnFeatures_NoCausaExcepcion()
        {
            var csv = "bar,open,high,low,close,volume,buy_volume,sell_volume,trade_count,mean_trade_size,ofi,buy_sell_ratio,cvd_delta,arrival_rate,price_return,cvd\n" +
                      $"{Bar1:yyyy-MM-dd HH:mm:sszzz},29000,29100,28900,29050,100,60,40,1200,0.05,nan,nan,nan,1200,0.002,nan\n";
            var path = WriteCsv(csv);
            _registry.Load(Btc, path);

            var result = _registry.GetBar(Btc, Bar1);

            result.Should().NotBeNull();
            double.IsNaN(result!.Ofi).Should().BeTrue();
            double.IsNaN(result.CvdDelta).Should().BeTrue();
        }
    }
}
