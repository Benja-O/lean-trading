using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Regimes;
using Xunit;

namespace Trading.Strategies.Tests.Regimes
{
    public class BinanceKlinesParserTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime WideStart = new(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime WideEnd = new(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // OpenTime=1577836800000 = 2020-01-01 00:00:00 UTC
        private const string RowJan01 = "1577836800000,7189.43,7239.74,7170.15,7221.65,12055.759,1577851199999,86901102.71235,19586,6356.286,45824354.14661,0";
        private const string RowJan02 = "1577851200000,7221.80,7230,7193.52,7205.26,5738.351,1577865599999,41410695.20475,10298,2855.460,20607525.79974,0";
        private const string Header = "open_time,open,high,low,close,volume,close_time,quote_volume,count,taker_buy_volume,taker_buy_quote_volume,ignore";

        [Fact]
        public void ParseTextReader_FilaValida_ParsearCorrectamenteOhlcv()
        {
            var parser = new BinanceKlinesParser(Btc);
            using var reader = new StringReader(RowJan01);

            var bars = parser.ParseTextReader(reader, WideStart, WideEnd);

            bars.Should().HaveCount(1);
            var bar = bars.Single();
            bar.InstrumentId.Should().Be(Btc);
            bar.Open.Should().Be(7189.43m);
            bar.High.Should().Be(7239.74m);
            bar.Low.Should().Be(7170.15m);
            bar.Close.Should().Be(7221.65m);
            bar.Volume.Should().Be(12055.759m);
        }

        [Fact]
        public void ParseTextReader_ArchivoConHeader_LoDescarta()
        {
            var parser = new BinanceKlinesParser(Btc);
            string content = Header + "\n" + RowJan01;
            using var reader = new StringReader(content);

            var bars = parser.ParseTextReader(reader, WideStart, WideEnd);

            bars.Should().HaveCount(1);
        }

        [Fact]
        public void ParseTextReader_ArchivoSinHeader_LoParseaCompleto()
        {
            var parser = new BinanceKlinesParser(Btc);
            string content = RowJan01 + "\n" + RowJan02;
            using var reader = new StringReader(content);

            var bars = parser.ParseTextReader(reader, WideStart, WideEnd);

            bars.Should().HaveCount(2);
        }

        [Fact]
        public void ParseTextReader_TimestampMsEpoch_SeConvierteADateTimeUtc()
        {
            var parser = new BinanceKlinesParser(Btc);
            using var reader = new StringReader(RowJan01);

            var bars = parser.ParseTextReader(reader, WideStart, WideEnd);

            var expected = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            bars.Single().TimestampUtc.Should().Be(expected);
            bars.Single().TimestampUtc.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void ParseZipFile_DescomprimeEnMemoriaYParseaTodasLasFilas()
        {
            var parser = new BinanceKlinesParser(Btc);
            string tempPath = Path.Combine(Path.GetTempPath(), $"binance_klines_test_{Guid.NewGuid():N}.zip");
            try
            {
                using (var fileStream = File.Create(tempPath))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("BTCUSDT-4h-2020-01.csv");
                    using var writer = new StreamWriter(entry.Open());
                    writer.WriteLine(RowJan01);
                    writer.WriteLine(RowJan02);
                }

                var bars = parser.ParseZipFile(tempPath, WideStart, WideEnd);

                bars.Should().HaveCount(2);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public void ParseTextReader_PrecioInvalido_LanzaFormatException()
        {
            var parser = new BinanceKlinesParser(Btc);
            string invalidRow = "1577836800000,abc,7239.74,7170.15,7221.65,12055.759,1577851199999,86901102.71235,19586,6356.286,45824354.14661,0";
            using var reader = new StringReader(invalidRow);

            Action act = () => parser.ParseTextReader(reader, WideStart, WideEnd);

            act.Should().Throw<FormatException>().WithMessage("*Open*");
        }

        [Fact]
        public void ParseTextReader_FiltraPorRangoDeFechas()
        {
            var parser = new BinanceKlinesParser(Btc);
            string content = RowJan01 + "\n" + RowJan02;
            using var reader = new StringReader(content);

            // Ventana que solo incluye la primera barra (2020-01-01 00:00 UTC).
            var narrowStart = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var narrowEnd = new DateTime(2020, 1, 1, 3, 0, 0, DateTimeKind.Utc);

            var bars = parser.ParseTextReader(reader, narrowStart, narrowEnd);

            bars.Should().HaveCount(1);
            bars.Single().TimestampUtc.Should().Be(narrowStart);
        }
    }
}
