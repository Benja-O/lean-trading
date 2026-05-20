using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Trading.Strategies.Adapters;
using Trading.Strategies.Tests.Fakes;

namespace Trading.Strategies.Tests.Adapters
{
    public class HealthchecksIoPingerTests
    {
        private const string ValidHcPingUrl = "https://hc-ping.com/test-uuid-1234";
        private const string ValidHealthchecksIoUrl = "https://healthchecks.io/ping/test-uuid-5678";

        private static (HealthchecksIoPinger pinger, FakeHttpMessageHandler handler, FakeTradingLogger logger, FakeClock clock)
            Build(string? url, HttpStatusCode status = HttpStatusCode.OK, Exception? throws = null)
        {
            var handler = new FakeHttpMessageHandler(status, throws);
            var httpClient = new HttpClient(handler);
            var logger = new FakeTradingLogger();
            var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc) };
            var pinger = new HealthchecksIoPinger(url, httpClient, clock, logger);
            return (pinger, handler, logger, clock);
        }

        // ===== Constructor — URL inválida o ausente =====

        [Fact]
        public async Task Constructor_WhenUrlIsNull_DisablesPingAndLogsWarning()
        {
            var (pinger, handler, logger, _) = Build(null);
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(0);
            logger.WarningEntries.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task Constructor_WhenUrlIsEmpty_DisablesPingAndLogsWarning()
        {
            var (pinger, handler, logger, _) = Build("");
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(0);
            logger.WarningEntries.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task Constructor_WhenUrlHasInvalidDomain_DisablesPingAndLogsWarning()
        {
            var (pinger, handler, logger, _) = Build("https://example.com/ping/uuid");
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(0);
            logger.WarningEntries.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        // ===== Constructor — URL válida =====

        [Fact]
        public async Task Constructor_WhenUrlIsHcPingCom_EnablesPing()
        {
            var (pinger, handler, _, _) = Build(ValidHcPingUrl);
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(1);
        }

        [Fact]
        public async Task Constructor_WhenUrlIsHealthchecksIo_EnablesPing()
        {
            var (pinger, handler, _, _) = Build(ValidHealthchecksIoUrl);
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(1);
        }

        // ===== PingAsync — manejo de errores =====

        [Fact]
        public async Task PingAsync_WhenNon2xxResponse_LogsWarningAndDoesNotThrow()
        {
            var (pinger, handler, logger, _) = Build(ValidHcPingUrl, HttpStatusCode.ServiceUnavailable);
            var act = async () => await pinger.PingAsync(CancellationToken.None);
            await act.Should().NotThrowAsync();
            handler.RequestCount.Should().Be(1);
            logger.WarningEntries.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task PingAsync_WhenHttpRequestException_LogsWarningAndDoesNotThrow()
        {
            var (pinger, _, logger, _) = Build(ValidHcPingUrl, throws: new HttpRequestException("network error"));
            var act = async () => await pinger.PingAsync(CancellationToken.None);
            await act.Should().NotThrowAsync();
            logger.WarningEntries.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task PingAsync_WhenOperationCancelled_LogsWarningAndDoesNotThrow()
        {
            var (pinger, _, logger, _) = Build(ValidHcPingUrl, throws: new OperationCanceledException());
            var act = async () => await pinger.PingAsync(CancellationToken.None);
            await act.Should().NotThrowAsync();
            logger.WarningEntries.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task PingAsync_OnSuccess_DoesNotThrow()
        {
            var (pinger, _, _, _) = Build(ValidHcPingUrl, HttpStatusCode.OK);
            var act = async () => await pinger.PingAsync(CancellationToken.None);
            await act.Should().NotThrowAsync();
        }

        // ===== Throttle =====

        [Fact]
        public async Task PingAsync_ThrottlesSecondCallWithin5Minutes()
        {
            var (pinger, handler, _, clock) = Build(ValidHcPingUrl);
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(1);

            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(1, "segunda llamada dentro del throttle de 5 minutos no debe enviar request");
        }

        [Fact]
        public async Task PingAsync_SendsRequestAfter5MinutesElapsed()
        {
            var (pinger, handler, _, clock) = Build(ValidHcPingUrl);
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(1);

            clock.UtcNow = clock.UtcNow.AddMinutes(6);
            await pinger.PingAsync(CancellationToken.None);
            handler.RequestCount.Should().Be(2, "después de 6 minutos el throttle expiró y debe enviar request");
        }
    }

    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly Exception? _throws;
        public int RequestCount { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, Exception? throws = null)
        {
            _statusCode = statusCode;
            _throws = throws;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_throws != null)
                return Task.FromException<HttpResponseMessage>(_throws);
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}
