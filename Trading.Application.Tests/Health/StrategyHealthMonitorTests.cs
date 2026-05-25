using System;
using FluentAssertions;
using Trading.Application.Eventing;
using Trading.Application.Health;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Health
{
    public class StrategyHealthMonitorTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private const string Id = "EmaCross_BTCUSDT_1h";
        private const string Id2 = "EmaCross_ETHUSDT_1h";

        private readonly FakeClock _clock = new() { UtcNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc) };
        private readonly FakeTradingLogger _logger = new();
        private readonly FakeOrderRouter _orderRouter = new();

        // U1 muy alto para no interferir; rolling thresholds con ventanas pequeñas para mantener tests breves.
        private StrategyHealthThresholds SafeThresholds(
            decimal u1 = 0.99m,
            decimal u2Fraction = 0.15m,
            int u2Days = 2,
            int pfSustained = 2,
            int expSustained = 2,
            int minToArm = 2,
            int windowTrades = 2,
            int windowDays = 30)
            => new StrategyHealthThresholds(
                absoluteDrawdownFromAthFraction: u1,
                rollingDrawdownThirtyDaysFraction: u2Fraction,
                rollingDrawdownSustainedDays: u2Days,
                rollingProfitFactorThreshold: 1.0m,
                rollingProfitFactorSustainedTrades: pfSustained,
                rollingExpectancyThreshold: 0m,
                rollingExpectancySustainedTrades: expSustained,
                minimumTradesToArmRollingThresholds: minToArm,
                rollingWindowTrades: windowTrades,
                rollingWindowDays: windowDays);

        private (StrategyHealthMonitor monitor, IDomainEventBus bus, CapturingEventSubscriber<RiskLimitBreachedEvent> capturer)
            Build(StrategyHealthThresholds? thresholds = null, decimal initialEquityPerStrategy = 0.000001m)
        {
            var eventBus = new DomainEventBus(_logger);
            var capturer = new CapturingEventSubscriber<RiskLimitBreachedEvent>(eventBus);
            var monitor = new StrategyHealthMonitor(
                thresholds ?? SafeThresholds(),
                _clock,
                _orderRouter,
                _logger,
                eventBus,
                initialEquityPerStrategy);
            return (monitor, eventBus, capturer);
        }

        private void Fill(IDomainEventBus bus, string id, OrderPurpose purpose, decimal price, decimal qty,
            InstrumentId? instrument = null)
            => bus.Publish(new OrderFilledEvent(
                TimestampUtc: _clock.UtcNow,
                ExecutorIdentifier: id,
                InstrumentId: instrument ?? Btc,
                Purpose: purpose,
                FillQuantity: qty,
                FillPrice: price));

        // ===== Trade lifecycle =====

        [Fact]
        public void OnEntry_AbrePosicionParaLaEstrategia()
        {
            var (monitor, bus, _) = Build();

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);

            monitor.IsExcluded(Id).Should().BeFalse();
            _logger.ErrorEntries.Should().BeEmpty();
        }

        [Fact]
        public void OnEntryConPosicionAbierta_LoguaErrorDeInvariante()
        {
            // El bus captura la excepción del handler y la loguea como Error.
            var (_, bus, _) = Build();

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.Entry, price: 110m, qty: +1m);

            _logger.ErrorEntries.Should().ContainSingle(e =>
                e.Arguments[1].ToString()!.Contains("OPS-2 invariante violado") &&
                e.Arguments[1].ToString()!.Contains("posición ya abierta"));
        }

        [Fact]
        public void OnCierreSinEntry_LoguaErrorDeInvariante()
        {
            var (_, bus, _) = Build();

            Fill(bus, Id, OrderPurpose.StopLoss, price: 95m, qty: -1m);

            _logger.ErrorEntries.Should().ContainSingle(e =>
                e.Arguments[1].ToString()!.Contains("OPS-2 invariante violado") &&
                e.Arguments[1].ToString()!.Contains("sin posición previa"));
        }

        [Fact]
        public void OnCierre_CalculaPnlRealizado_CasoLong()
        {
            // Entry Long @100 qty=+1, SL @95 → PnL = (95-100)*1*1 = -5
            // Capital grande para que la pérdida no dispare U1 (DD=5/10000=0.05%<<99%).
            var (monitor, bus, _) = Build(initialEquityPerStrategy: 10_000m);

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 95m, qty: -1m);

            monitor.IsExcluded(Id).Should().BeFalse();
            _logger.ErrorEntries.Should().BeEmpty();
        }

        [Fact]
        public void OnCierre_CalculaPnlRealizado_CasoShort()
        {
            // Entry Short @100 qty=-1, TP @95 → PnL = (95-100)*1*(-1) = +5
            var (monitor, bus, _) = Build();

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: -1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 95m, qty: +1m);

            monitor.IsExcluded(Id).Should().BeFalse();
            _logger.ErrorEntries.Should().BeEmpty();
        }

        [Fact]
        public void OnCierre_CalculaPnlRealizado_TimeExitConGanancia()
        {
            // Entry Long @100 qty=+1, TimeExit @110 → PnL = +10
            var (monitor, bus, _) = Build();

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TimeExit, price: 110m, qty: -1m);

            monitor.IsExcluded(Id).Should().BeFalse();
            _logger.ErrorEntries.Should().BeEmpty();
        }

        [Fact]
        public void OnCierre_ConBalancePositivo_ActualizaAth()
        {
            // Win +100, luego break-even. ATH=100. Con U1=99%, no dispara.
            var (monitor, bus, _) = Build(SafeThresholds(u1: 0.99m));

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 200m, qty: -1m); // PnL=+100, ATH=100

            Fill(bus, Id, OrderPurpose.Entry, price: 50m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 50m, qty: -1m); // PnL=0, equity=100, DD=0%

            monitor.IsExcluded(Id).Should().BeFalse();
            _logger.CriticalEntries.Should().BeEmpty();
        }

        // ===== U1 (DD absoluto desde ATH) =====

        [Fact]
        public void U1_PorDebajoDelUmbral_NoDispara()
        {
            // U1=25%. Win +100 → ATH=100. Loss -24 → equity=76, DD=24% < 25%. No dispara.
            var (monitor, bus, capturer) = Build(SafeThresholds(u1: 0.25m));

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 200m, qty: -1m); // PnL=+100

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 76m, qty: -1m); // PnL=-24, equity=76, DD=24%

            monitor.IsExcluded(Id).Should().BeFalse();
            capturer.CapturedEvents.Should().BeEmpty();
        }

        [Fact]
        public void U1_SuperaUmbral_DisparaConPosicionCerrada()
        {
            // U1=25%. Win +100 → ATH=100. Loss -26 → equity=74, DD=26% > 25% → TRIGGER.
            // En el instante del breach la posición ya está cerrada: LiquidateInstrument NO se llama.
            var (monitor, bus, capturer) = Build(SafeThresholds(u1: 0.25m));

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 200m, qty: -1m); // PnL=+100

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 74m, qty: -1m); // PnL=-26, DD=26%

            monitor.IsExcluded(Id).Should().BeTrue();
            capturer.CapturedEvents.Should().ContainSingle(e =>
                e.Reason == RiskLimitBreachReason.StrategyDegradation);
            _orderRouter.LiquidateInstrumentCalls.Should().BeEmpty();
        }

        [Fact]
        public void U1_DespuesDeDisparar_PosterioresEventosSeIgnoran()
        {
            var (monitor, bus, capturer) = Build(SafeThresholds(u1: 0.25m));

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 200m, qty: -1m);
            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 74m, qty: -1m); // dispara U1

            Fill(bus, Id, OrderPurpose.Entry, price: 50m, qty: +1m); // debe ser ignorado silenciosamente

            capturer.CapturedEvents.Should().HaveCount(1);
            _logger.ErrorEntries.Should().BeEmpty();
        }

        // ===== U2 (DD rolling 30 días sostenido N días) =====

        [Fact]
        public void U2_DdRollingBajoUmbral_NoDispara()
        {
            // Win +100 → ATH=100. Luego pérdidas diarias de -10 (DD=10% < 15%). Counter no llega a 2.
            // Tercer día: DD=20%>15% → counter=1 (no trigger, necesita 2).
            var (monitor, bus, capturer) = Build(SafeThresholds(u1: 0.99m, u2Fraction: 0.15m, u2Days: 2));

            // entry @10 qty=+1, exit @110 → PnL = (110-10)*1*1 = +100
            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 110m, qty: -1m); // equity=100, ATH=100

            // entry @10 qty=+1, SL @0 → PnL = (0-10)*1*1 = -10
            _clock.Advance(TimeSpan.FromDays(1));
            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 0m, qty: -1m); // equity=90, DD=10% → counter=0

            _clock.Advance(TimeSpan.FromDays(1));
            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 0m, qty: -1m); // equity=80, DD=20% → counter=1

            monitor.IsExcluded(Id).Should().BeFalse();
            capturer.CapturedEvents.Should().BeEmpty();
        }

        [Fact]
        public void U2_DdRollingSobreUmbralPorDiasRequeridos_Dispara()
        {
            // Win +100. Luego 3 días consecutivos de pérdidas:
            // día 2: DD=10% → counter=0 | día 3: DD=20%>15% → counter=1 | día 4: DD=30%>15% → counter=2 → TRIGGER
            var (monitor, bus, capturer) = Build(SafeThresholds(u1: 0.99m, u2Fraction: 0.15m, u2Days: 2));

            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 110m, qty: -1m); // equity=100, ATH=100

            _clock.Advance(TimeSpan.FromDays(1));
            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 0m, qty: -1m); // equity=90, DD=10%

            _clock.Advance(TimeSpan.FromDays(1));
            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 0m, qty: -1m); // equity=80, DD=20% → counter=1

            _clock.Advance(TimeSpan.FromDays(1));
            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 0m, qty: -1m); // equity=70, DD=30% → counter=2 → TRIGGER

            monitor.IsExcluded(Id).Should().BeTrue();
            capturer.CapturedEvents.Should().ContainSingle(e =>
                e.Reason == RiskLimitBreachReason.StrategyDegradation &&
                e.Description.Contains("U2"));
        }

        [Fact]
        public void U2_DdRollingBajaAlUmbralEnElMedio_ResetaContador()
        {
            // Win +100. Día 2: DD>15% → counter=1. Día 3: recuperación, DD<15% → counter=0.
            // Día 4: DD>15% → counter=1 (no trigger, porque se resetó). Necesita 2 consecutivos.
            var (monitor, bus, capturer) = Build(SafeThresholds(u1: 0.99m, u2Fraction: 0.15m, u2Days: 2));

            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 110m, qty: -1m); // equity=100, ATH=100

            // Día 2: pérdida grande → DD>15% → counter=1
            _clock.Advance(TimeSpan.FromDays(1));
            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 0m, qty: -1m); // equity=90, DD=10% … hmm

            // Con ATH=100 y equity=90, DD=10%. No supera 15%.
            // Para que el día 2 tenga DD>15% necesito que equity < 85.
            // Uso entry @20 qty=+1, SL @0 → PnL=-20, equity=80, DD=20%>15%
            // Rehacer en un nuevo Build para que los números cuadren.
            var (monitor2, bus2, capturer2) = Build(SafeThresholds(u1: 0.99m, u2Fraction: 0.15m, u2Days: 2));
            var clock2 = _clock; // reutilizo el mismo clock (ya está en 2025-01-01 desde el setUp)
            // El clock global ya fue avanzado en el bloque anterior. Creo un nuevo FakeClock local.
            var lClock = new FakeClock { UtcNow = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc) };
            var lLogger = new FakeTradingLogger();
            var lRouter = new FakeOrderRouter();
            var lBus = new DomainEventBus(lLogger);
            var lCapturer = new CapturingEventSubscriber<RiskLimitBreachedEvent>(lBus);
            var lMonitor = new StrategyHealthMonitor(
                SafeThresholds(u1: 0.99m, u2Fraction: 0.15m, u2Days: 2),
                lClock, lRouter, lLogger, lBus,
                // Capital inicial despreciable: simula la convención antigua donde el monitor
                // partía de equity=0. Los tests están escritos en términos de PnL crudo y ese
                // contrato se preserva.
                initialEquityPerStrategy: 0.000001m);

            void LFill(string eid, OrderPurpose p, decimal price, decimal qty)
                => lBus.Publish(new OrderFilledEvent(lClock.UtcNow, eid, Btc, p, qty, price));

            // entry @20 qty=+1, TP @120 → PnL=+100 (equity=100, ATH=100)
            LFill(Id, OrderPurpose.Entry, 20m, +1m);
            LFill(Id, OrderPurpose.TakeProfit, 120m, -1m);

            // Día 2: entry @20 qty=+1, SL @0 → PnL=-20, equity=80, DD=20%>15% → counter=1
            lClock.Advance(TimeSpan.FromDays(1));
            LFill(Id, OrderPurpose.Entry, 20m, +1m);
            LFill(Id, OrderPurpose.StopLoss, 0m, -1m);

            // Día 3: recuperación. entry @0.1 qty=+1, TP @20 → PnL=+19.9, equity=99.9, DD≈0.1%<15% → counter=0
            lClock.Advance(TimeSpan.FromDays(1));
            LFill(Id, OrderPurpose.Entry, 0m, +1m); // entry @0 (puede ser 0)
            // PnL = (20-0)*1*1 = +20, equity=100, DD=0% → counter=0
            LFill(Id, OrderPurpose.TakeProfit, 20m, -1m);

            // Día 4: pérdida → DD>15% → counter=1 (resetó en día 3, no llega a 2)
            lClock.Advance(TimeSpan.FromDays(1));
            LFill(Id, OrderPurpose.Entry, 20m, +1m);
            LFill(Id, OrderPurpose.StopLoss, 0m, -1m); // equity=80, DD=20%>15% → counter=1 < u2Days=2

            lMonitor.IsExcluded(Id).Should().BeFalse("el contador se reseteó en el día 3");
            lCapturer.CapturedEvents.Should().BeEmpty();
        }

        [Fact]
        public void U2_VariosTradesElMismoDia_NoAcumulaPuntosDiarios()
        {
            // Con u2Days=1 y u2Fraction=0.01m (1%), si hubiera 2 puntos en un día con DD>1%
            // dispararía. Pero al ser el mismo día solo hay 1 punto → U2 requiere ≥2 → no dispara.
            var (monitor, bus, capturer) = Build(SafeThresholds(u1: 0.99m, u2Fraction: 0.01m, u2Days: 1));

            Fill(bus, Id, OrderPurpose.Entry, price: 10m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 110m, qty: -1m); // equity=100

            // 2 pérdidas en el mismo día (clock no avanza)
            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 90m, qty: -1m); // equity=90, DD=10%>1%

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 90m, qty: -1m); // equity=80, mismo día

            // Solo 1 punto diario acumulado. EvaluateU2 devuelve false (Count < 2).
            monitor.IsExcluded(Id).Should().BeFalse();
            capturer.CapturedEvents.Should().BeEmpty();
        }

        // ===== U3 (PF rolling sostenido N trades) =====

        [Fact]
        public void U3_MenosDeLosTradesMinimosParaArmar_NoSeEvalua()
        {
            // minToArm=3, windowTrades=2, pfSustained=1. Tras 2 pérdidas (total<3): no armado, no dispara.
            // Capital grande para que pérdidas de -20 no disparen U1 (DD=40/10000=0.4%<<99%).
            var (monitor, bus, capturer) = Build(SafeThresholds(
                u1: 0.99m, minToArm: 3, windowTrades: 2, pfSustained: 1),
                initialEquityPerStrategy: 10_000m);

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m);

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m);

            monitor.IsExcluded(Id).Should().BeFalse();
            capturer.CapturedEvents.Should().BeEmpty();
            _logger.InfoEntries.Should().NotContain(e => e.MessageTemplate.Contains("armados"));
        }

        [Fact]
        public void U3_AlAlcanzarElMinimoDeArmado_LogueaInformacion()
        {
            // minToArm=2. Trade #2 → arm → log Information con ExecutorIdentifier.
            var (_, bus, _) = Build(SafeThresholds(minToArm: 2, windowTrades: 2));

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 110m, qty: -1m);

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 110m, qty: -1m); // trade #2 → arm

            _logger.InfoEntries.Should().Contain(e =>
                e.MessageTemplate.Contains("armados") &&
                e.Arguments[0].ToString() == Id);
        }

        [Fact]
        public void U3_ConPfSobre1_NoDispara()
        {
            // Todos los trades son ganadores → PF = grossProfit/0 → skip (grossLoss=0) → no dispara.
            var (monitor, bus, capturer) = Build(SafeThresholds(
                u1: 0.99m, minToArm: 2, windowTrades: 2, pfSustained: 1));

            for (int i = 0; i < 3; i++)
            {
                Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
                Fill(bus, Id, OrderPurpose.TakeProfit, price: 110m, qty: -1m);
            }

            monitor.IsExcluded(Id).Should().BeFalse();
            capturer.CapturedEvents.Should().BeEmpty();
        }

        [Fact]
        public void U3_ConPfBajoPorSustainedTrades_Dispara()
        {
            // minToArm=2, windowTrades=2, pfSustained=2.
            // Trade#2: arm, eval PF=0<1, counter=1. Trade#3: eval PF=0<1, counter=2 → TRIGGER.
            // Capital grande para que pérdidas de -20 no disparen U1 (DD=60/10000=0.6%<<99%).
            var (monitor, bus, capturer) = Build(SafeThresholds(
                u1: 0.99m, minToArm: 2, windowTrades: 2, pfSustained: 2),
                initialEquityPerStrategy: 10_000m);

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#1 → not armed

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#2 → arm, counter=1

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#3 → counter=2 → TRIGGER

            monitor.IsExcluded(Id).Should().BeTrue();
            capturer.CapturedEvents.Should().ContainSingle(e =>
                e.Reason == RiskLimitBreachReason.StrategyDegradation &&
                e.Description.Contains("U3"));
        }

        [Fact]
        public void U3_ConGrossLossCero_NoEvalua_YResetaContador()
        {
            // minToArm=2, windowTrades=2, pfSustained=3, expSustained=100 (U4 deshabilitado).
            // Trade#2: arm, 2 pérdidas en ventana → PF=0<1, counter=1.
            // Trade#3: pérdida → PF=0<1, counter=2. No trigger (need 3).
            // Trade#4: ganancia → ventana=[pérdida, ganancia], PF>1 → counter=0. No dispara.
            // Capital grande para que pérdidas de -20 no disparen U1 (DD=60/10000=0.6%<<99%).
            var (monitor, bus, capturer) = Build(SafeThresholds(
                u1: 0.99m, minToArm: 2, windowTrades: 2, pfSustained: 3, expSustained: 100),
                initialEquityPerStrategy: 10_000m);

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#1, not armed

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#2, arm, counter=1

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#3, counter=2

            // Trade#4: ganancia grande → PF = 100/20 = 5 > 1.0 → counter=0
            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 200m, qty: -1m); // PnL=+100, ventana=[loss,win], PF>1

            monitor.IsExcluded(Id).Should().BeFalse();
            capturer.CapturedEvents.Should().BeEmpty();
        }

        // ===== U4 (expectancy rolling sostenido N trades) =====

        [Fact]
        public void U4_ConExpectancyNegativaSostenida_Dispara()
        {
            // U3 deshabilitado con pfSustained=100. minToArm=2, windowTrades=2, expSustained=2.
            // Trade#2: arm, eval U3 counter=1 (<100), eval U4 exp<0 counter=1. Trade#3: U4 counter=2 → TRIGGER.
            // Capital grande para que pérdidas de -20 no disparen U1 (DD=60/10000=0.6%<<99%).
            var (monitor, bus, capturer) = Build(SafeThresholds(
                u1: 0.99m, u2Fraction: 0.99m,
                minToArm: 2, windowTrades: 2,
                pfSustained: 100, expSustained: 2),
                initialEquityPerStrategy: 10_000m);

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#1

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#2 → arm, U4 counter=1

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 80m, qty: -1m); // trade#3 → U4 counter=2 → TRIGGER

            monitor.IsExcluded(Id).Should().BeTrue();
            capturer.CapturedEvents.Should().ContainSingle(e =>
                e.Reason == RiskLimitBreachReason.StrategyDegradation &&
                e.Description.Contains("U4"));
        }

        // ===== Multi-estrategia y exclusión =====

        [Fact]
        public void IsExcluded_EstrategiaNoConocida_RetornaFalse()
        {
            var (monitor, _, _) = Build();

            monitor.IsExcluded("EstrategiaDesconocida_BTC_1h").Should().BeFalse();
        }

        [Fact]
        public void IsExcluded_DespuesDeDegradacion_RetornaTrue()
        {
            var (monitor, bus, _) = Build(SafeThresholds(u1: 0.25m));

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 200m, qty: -1m);
            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 74m, qty: -1m);

            monitor.IsExcluded(Id).Should().BeTrue();
        }

        [Fact]
        public void DosEstrategiasIndependientes_UnaDegradadaNoAfectaALaOtra()
        {
            var (monitor, bus, _) = Build(SafeThresholds(u1: 0.25m));

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 200m, qty: -1m);
            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 74m, qty: -1m); // Id degradada

            monitor.IsExcluded(Id).Should().BeTrue();
            monitor.IsExcluded(Id2).Should().BeFalse();
        }

        // ===== RiskLimitBreachedEvent =====

        [Fact]
        public void AlDispararUmbral_PublicaEventoConStrategyDegradationYDescripcionConId()
        {
            var (_, bus, capturer) = Build(SafeThresholds(u1: 0.25m));

            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.TakeProfit, price: 200m, qty: -1m);
            Fill(bus, Id, OrderPurpose.Entry, price: 100m, qty: +1m);
            Fill(bus, Id, OrderPurpose.StopLoss, price: 74m, qty: -1m);

            capturer.CapturedEvents.Should().ContainSingle(e =>
                e.Reason == RiskLimitBreachReason.StrategyDegradation &&
                e.Description.Contains(Id) &&
                e.Description.Contains("U1"));
        }
    }
}
