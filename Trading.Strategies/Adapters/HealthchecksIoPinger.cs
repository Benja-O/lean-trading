using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Envía un GET al endpoint de Healthchecks.io como dead-man's switch.
    /// Throttle interno: máximo un ping cada 5 minutos (wall clock del backtest/live).
    /// Nunca lanza excepción al caller. Thread-safe: _lastPingUtc es volátil.
    /// </summary>
    public sealed class HealthchecksIoPinger
    {
        private static readonly Regex ValidUrlPattern =
            new(@"^https://(hc-ping\.com|healthchecks\.io)/.+", RegexOptions.IgnoreCase);

        private readonly string? _pingUrl;
        private readonly HttpClient _httpClient;
        private readonly IClock _clock;
        private readonly ITradingLogger _logger;
        private readonly bool _enabled;
        private DateTime _lastPingUtc = DateTime.MinValue;

        public HealthchecksIoPinger(
            string? pingUrl,
            HttpClient httpClient,
            IClock clock,
            ITradingLogger logger)
        {
            _httpClient = httpClient;
            _clock = clock;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(pingUrl))
            {
                _enabled = false;
                _logger.Warning("HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada. Pings deshabilitados.");
                return;
            }

            if (!ValidUrlPattern.IsMatch(pingUrl))
            {
                _enabled = false;
                _logger.Warning(
                    "HealthchecksIoPinger: URL '{PingUrl}' no es un endpoint válido de Healthchecks.io. Pings deshabilitados.",
                    pingUrl);
                return;
            }

            _pingUrl = pingUrl;
            _enabled = true;
        }

        public async Task PingAsync(CancellationToken ct)
        {
            if (!_enabled) return;

            var now = _clock.UtcNow;
            if (now - _lastPingUtc < TimeSpan.FromMinutes(5)) return;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                var response = await _httpClient.GetAsync(_pingUrl, cts.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    _lastPingUtc = now;
                }
                else
                {
                    _logger.Warning(
                        "HealthchecksIoPinger: respuesta no-2xx {StatusCode}",
                        (int)response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Warning(
                    "HealthchecksIoPinger: HttpRequestException {Message}",
                    ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("HealthchecksIoPinger: ping cancelado o timeout (10s).");
            }
        }
    }
}
