using System;

namespace Trading.Application.Health
{
    /// <summary>
    /// Snapshot inmutable del estado de salud del sistema en un instante.
    /// Serializado a JSON por HeartbeatFileWriter. Todos los campos nullable
    /// reflejan "todavía no ocurrió" (estado normal al inicio del proceso).
    /// </summary>
    public sealed record HealthSnapshot(
        DateTime CurrentUtc,
        DateTime ProcessStartedUtc,
        DateTime? LastBarProcessedUtc,
        DateTime? LastBarTimestampUtc,
        DateTime? LastOrderSubmittedUtc,
        DateTime? LastOrderFilledUtc,
        DateTime? LastRiskBreachUtc,
        string? LastRiskBreachReason,
        bool KillSwitchActive,
        // Segundos transcurridos desde la última barra procesada (null = aún en warmup).
        // Expuesto en heartbeat.json para revisión diaria del operador (POLICY 2.4).
        double? BarStalenessSeconds,
        // Wall clock del último dato de minuto recibido del feed (null = aún en warmup).
        // Distinto de LastBarProcessedUtc: mide liveness del WebSocket (cadencia ~1min),
        // no el cierre de barras de estrategia (cadencia del timeframe, p.ej. 1h).
        DateTime? LastDataReceivedUtc,
        // Segundos desde el último dato de minuto (null = aún en warmup). Lo consume el
        // auto-restart del host: detecta un WebSocket congelado sin importar el timeframe.
        double? DataFeedStalenessSeconds,
        // Mensaje del último fallo de IO del sink JSONL (null = sin fallos conocidos).
        // Se propaga desde JsonlFileLogSink.LastWriteFailure vía el host cada 60s.
        string? SinkLastWriteFailureMessage);
}
