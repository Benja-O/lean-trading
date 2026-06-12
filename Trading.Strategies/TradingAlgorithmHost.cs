using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Slippage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Trading.Application.Eventing;
using Trading.Application.Execution;
using Trading.Application.Health;
using Trading.Application.Microstructure;
using Trading.Application.Regimes;
using Trading.Application.Risk;
using Trading.Application.Sizing;
using Trading.Domain.Abstractions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Adapters;
using Trading.Strategies.Infrastructure;
using Trading.Strategies.Regimes;

namespace Trading.Strategies
{
    /// <summary>
    /// Host del sistema. Es el ÚNICO lugar que extiende QCAlgorithm y compone los adaptadores Lean
    /// con los servicios de Trading.Application.
    ///
    /// Responsabilidades:
    /// 1. Configuración del backtest/live (fechas, cash, brokerage, símbolos).
    /// 2. Construcción de adaptadores Lean (resolver, portfolio, metadata, router, clock, logger).
    /// 3. Construcción de servicios de Application (RiskOrchestrator, Sizer, BarProcessing, OrderLifecycle).
    /// 4. Wiring de consolidators -> BarProcessingService.
    /// 5. Routing de OrderEvent -> OrderLifecycleService vía OrderEventMapper.
    ///
    /// La lógica de negocio NO vive aquí; vive en Trading.Application sin conocer Lean.
    /// </summary>
    public class TradingAlgorithmHost : QCAlgorithm
    {
        private const decimal InitialAccountCashUsdt = 100_000m;

        // Umbral de staleness para el dead-man's switch. Si no se procesa ninguna barra
        // dentro de esta ventana (wall-clock), el ping se suprime y Healthchecks.io cae a DOWN.
        // 90 min: mayor que la barra procesable más amplia activa (hoy 15m; cubre hasta 30m/1h
        // con jitter de feed) sin tener que recalibrar cuando strategies.json cambia.
        // Detecta de sobra el freeze real que motivó Brief B (~24 h de inactividad).
        private static readonly TimeSpan BarStalenessThreshold = TimeSpan.FromMinutes(90);

        private readonly StrategyConfigLoader _strategyConfigurationLoader = new();
        private readonly List<StrategyExecutor> _strategyExecutors = new();

        // Adaptadores Lean
        private LeanInstrumentResolver _instrumentResolver;
        private IPortfolioState _portfolioState;
        private IInstrumentMetadata _instrumentMetadata;
        private IOrderRouter _orderRouter;
        private IClock _clock;
        private ITradingLogger _logger;
        private IPriceRounder _priceRounder;

        // Observabilidad
        private JsonlFileLogSink _structuredLogSink;
        private HealthHeartbeatTracker _healthHeartbeatTracker;
        private HeartbeatFileWriter _heartbeatFileWriter;
        private System.Threading.Timer _heartbeatFlushTimer;
        private HttpClient _httpClient;
        private HealthchecksIoPinger _healthchecksPinger;
        private BarStalenessGate _barStalenessGate;

        // Servicios de Application
        private OrderRegistry _orderRegistry;
        private RiskOrchestrator _riskOrchestrator;
        private DrawdownMonitor _drawdownMonitor;
        private ConsecutiveLossesMonitor _consecutiveLossesMonitor;
        private PositionSizer _positionSizer;
        private BarProcessingService _barProcessingService;
        private OrderLifecycleService _orderLifecycleService;

        public override void Initialize()
        {
            // ===== Adaptadores Lean =====
            _instrumentResolver = new LeanInstrumentResolver();
            _portfolioState = new LeanPortfolioAdapter(this, _instrumentResolver);
            _instrumentMetadata = new LeanInstrumentMetadataAdapter(this, _instrumentResolver);
            _orderRegistry = new OrderRegistry();
            _clock = new LeanClock(this);
            // logs/trading-{fecha}.jsonl relativo al directorio de salida, retención 30 días
            _structuredLogSink = new JsonlFileLogSink(_clock);
            _logger = new LeanLogger(this, _structuredLogSink);
            _orderRouter = new LeanOrderRouter(this, _instrumentResolver, _orderRegistry, _portfolioState, _logger);
            _priceRounder = new PriceRounder(_instrumentMetadata);

            // ===== Servicios de Application =====
            var domainEventBus = new DomainEventBus(_logger);

            // Heartbeat: suscripto a eventos de dominio antes de que se emitan
            _healthHeartbeatTracker = new HealthHeartbeatTracker(domainEventBus, _clock, _logger);
            _heartbeatFileWriter = new HeartbeatFileWriter(_healthHeartbeatTracker, _clock, _logger);
            var pingUrl = Environment.GetEnvironmentVariable("HEALTHCHECKS_PING_URL");
            _httpClient = new HttpClient();
            _healthchecksPinger = new HealthchecksIoPinger(pingUrl, _httpClient, _clock, _logger);
            _barStalenessGate = new BarStalenessGate(_healthHeartbeatTracker, BarStalenessThreshold);

            // ===== Carga y validación de configuración =====
            string strategiesFilePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "strategies.json");
            var rootConfiguration = _strategyConfigurationLoader.Load(strategiesFilePath);

            _drawdownMonitor = new DrawdownMonitor(_portfolioState, 0.25m);
            _consecutiveLossesMonitor = new ConsecutiveLossesMonitor(8);
            var activeInstruments = ExtractActiveInstruments(rootConfiguration);
            var riskAction = new LiquidateAllRiskAction(_orderRouter, _portfolioState, activeInstruments);
            var coolingOffTracker = new CoolingOffTracker(_clock, TimeSpan.FromDays(1));
            _riskOrchestrator = new RiskOrchestrator(
                new IRiskMonitor[] { _drawdownMonitor, _consecutiveLossesMonitor },
                riskAction, coolingOffTracker, _clock, _logger, domainEventBus);
            _positionSizer = new PositionSizer(_portfolioState, _instrumentMetadata, _logger);

            // ===== Configuración del entorno de trading =====
            SetAccountCurrency("USDT");

            if (!LiveMode)
            {
                // Backtest: ventana temporal y cash simulado.
                SetStartDate(2021, 1, 1);
                SetEndDate(2024, 12, 31);
                SetCash("USDT", InitialAccountCashUsdt);
                SetCash("USD", 0);
            }
            // En live, el cash y el rango temporal los provee el brokerage / wall clock.
            // DrawdownMonitor se inicializa en OnWarmupFinished(), no aquí:
            // en live mode el broker carga el balance real DESPUÉS de Initialize(),
            // por lo que capturarlo aquí daría el default de Lean (~100k) como high-water mark.

            this.SetBrokerageModel(BrokerageName.Binance, AccountType.Margin);
            SetBenchmark(x => 0);

            // ===== Registro de instrumentos =====
            var symbolsToLoad = new HashSet<string>();
            foreach (var timeframeNode in rootConfiguration.Timeframes)
            {
                foreach (var strategyDefinition in timeframeNode.Value.Strategies)
                {
                    symbolsToLoad.Add(strategyDefinition.Symbol);
                }
            }

            foreach (var symbolTicker in symbolsToLoad)
            {
                var cryptoAsset = AddCryptoFuture(symbolTicker, Resolution.Minute, Market.Binance);
                cryptoAsset.SetFeeModel(new ConstantFeeModel(0.001m, "USDT"));
                cryptoAsset.SetSlippageModel(new ConstantSlippageModel(0.001m));
                _instrumentResolver.Register(cryptoAsset.Symbol);
            }

            // ===== Régimen de mercado (HMM real, Paso 3 del Hito B) =====
            // Wiring agnóstico al instrumento: extrae dinámicamente del strategies.json los instrumentos
            // únicos que tienen al menos una estrategia con CompatibleRegimes declarado, y carga el modelo
            // serializado correspondiente. Si una estrategia depende del régimen pero el modelo no existe,
            // el sistema falla loud al boot (fail-fast).
            var instrumentsRequiringRegime = ExtractInstrumentsRequiringRegime(rootConfiguration);
            var regimeClassifiers = new List<IMarketRegimeClassifier>();
            foreach (var instrumentId in instrumentsRequiringRegime)
            {
                string modelPath = System.IO.Path.Combine(
                    System.AppContext.BaseDirectory, "models", "regime",
                    $"{instrumentId.Ticker}-perp-binance.hmm.json");
                if (!File.Exists(modelPath))
                {
                    throw new InvalidOperationException(
                        $"El instrumento {instrumentId.Ticker} tiene estrategias con CompatibleRegimes declarado " +
                        $"pero no existe el modelo entrenado en '{modelPath}'. " +
                        "Ejecutá HmmTrainer para generarlo.");
                }
                regimeClassifiers.Add(AccordHmmClassifierFactory.Load(modelPath));
            }
            var regimeRegistry = new MarketRegimeRegistry(regimeClassifiers, _clock, _logger);

            // ===== Features microestructurales (E-INFRA-2) =====
            // Carga opcional: si el CSV no existe para un símbolo, el registry devuelve null y las
            // estrategias OHLCV-only (EmaCrossStrategy) no se ven afectadas. Solo las estrategias
            // microestructurales (Hito E) degradan a Flat cuando el proveedor retorna null.
            var microstructureRegistry = new MicrostructureRegistry(_logger);
            string microstructureDir = rootConfiguration.MicrostructureDataPath
                ?? System.IO.Path.Combine(System.AppContext.BaseDirectory, "microstructure");
            foreach (var symbolTicker in symbolsToLoad)
            {
                string csvPath = System.IO.Path.Combine(
                    microstructureDir, $"{symbolTicker}_1h_features.csv");
                microstructureRegistry.Load(new InstrumentId(symbolTicker), csvPath);
            }

            // ===== Construcción de executors =====
            var strategyCompatibilities = new Dictionary<string, StrategyRegimeCompatibility>();

            foreach (var timeframeNode in rootConfiguration.Timeframes)
            {
                string timeframe = timeframeNode.Key;
                TimeSpan timeframeSpan = TimeframeHelper.GetTimeSpan(timeframe);

                var strategiesBySymbol = timeframeNode.Value.Strategies
                    .GroupBy(strategy => strategy.Symbol);

                foreach (var strategyGroup in strategiesBySymbol)
                {
                    string symbolTicker = strategyGroup.Key;
                    var instrumentId = new InstrumentId(symbolTicker);
                    var symbol = _instrumentResolver.Resolve(instrumentId);

                    var tradeBarConsolidator = new TradeBarConsolidator(timeframeSpan);
                    var localStrategyExecutors = new List<StrategyExecutor>();

                    foreach (var strategyDefinition in strategyGroup)
                    {
                        var strategy = StrategyFactory.Create(strategyDefinition.StrategyName, microstructureRegistry);

                        var riskParameters = RiskParameters.FromPercentages(
                            stopLossPercentage: strategyDefinition.StopLossPercentage,
                            takeProfitPercentage: strategyDefinition.TakeProfitPercentage,
                            riskPerTradePercentage: strategyDefinition.RiskPerTradePercentage.Value);

                        var strategyExecutor = new StrategyExecutor(
                            strategyDefinition, timeframe, instrumentId, strategy, riskParameters);
                        _strategyExecutors.Add(strategyExecutor);
                        localStrategyExecutors.Add(strategyExecutor);

                        IReadOnlySet<RegimeLabel> allowedRegimes = null;
                        if (strategyDefinition.CompatibleRegimes != null)
                        {
                            var parsedLabels = new HashSet<RegimeLabel>();
                            foreach (var regimeName in strategyDefinition.CompatibleRegimes)
                            {
                                try
                                {
                                    parsedLabels.Add(RegimeLabelParser.Parse(regimeName));
                                }
                                catch (ArgumentException parseException)
                                {
                                    throw new InvalidOperationException(
                                        $"Estrategia '{strategyExecutor.ExecutorIdentifier}' (timeframe {timeframe}, símbolo {symbolTicker}): " +
                                        $"valor inválido en CompatibleRegimes. {parseException.Message}", parseException);
                                }
                            }
                            allowedRegimes = parsedLabels;
                        }
                        strategyCompatibilities[strategyExecutor.ExecutorIdentifier] =
                            new StrategyRegimeCompatibility(strategyExecutor.ExecutorIdentifier, allowedRegimes);
                    }

                    tradeBarConsolidator.DataConsolidated += (sender, tradeBarData) =>
                    {
                        var marketBar = MarketBarMapper.ToMarketBar(
                            (TradeBar)tradeBarData, _instrumentResolver);
                        _barProcessingService.ProcessBar(marketBar, localStrategyExecutors, IsWarmingUp);
                    };

                    SubscriptionManager.AddConsolidator(symbol, tradeBarConsolidator);
                }
            }

            // ===== Consolidator dedicado para el régimen de mercado (4h) =====
            // Independiente de los consolidators de estrategias. Alimenta al MarketRegimeRegistry.
            // El HMM real necesita procesar barras DURANTE el warm-up de QC para calentar su buffer
            // interno (100 barras 4h), por eso NO chequeamos IsWarmingUp dentro del handler.
            foreach (var regimeInstrumentId in regimeRegistry.GetRegisteredInstruments())
            {
                var regimeSymbol = _instrumentResolver.Resolve(regimeInstrumentId);
                var regimeConsolidator = new TradeBarConsolidator(TimeSpan.FromHours(4));

                regimeConsolidator.DataConsolidated += (sender, tradeBarData) =>
                {
                    var marketBar = MarketBarMapper.ToMarketBar((TradeBar)tradeBarData, _instrumentResolver);
                    regimeRegistry.ClassifyBar(marketBar);
                };

                SubscriptionManager.AddConsolidator(regimeSymbol, regimeConsolidator);
            }

            // ===== Servicios que requieren la lista de executors ya armada =====
            var strategyHealthThresholds = StrategyHealthThresholds.FromPolicyDefaults();

            // Capital atribuido al monitor de salud por estrategia. Usamos la constante
            // del cash inicial porque Portfolio.TotalPortfolioValue todavía es 0 durante
            // Initialize() (Lean refleja el SetCash recién después de que Initialize
            // retorna). En la fase actual hay 1 estrategia activa por backtest, así que
            // toda la cuenta es suya.
            // TODO: cuando exista allocator multi-estrategia, atribuir por executor.
            var strategyHealthMonitor = new StrategyHealthMonitor(
                strategyHealthThresholds,
                _clock,
                _orderRouter,
                _logger,
                domainEventBus,
                InitialAccountCashUsdt);
            _barProcessingService = new BarProcessingService(
                _portfolioState, _orderRouter, _riskOrchestrator, _positionSizer,
                _logger, domainEventBus, _clock,
                regimeRegistry, strategyCompatibilities, strategyHealthMonitor);

            _orderLifecycleService = new OrderLifecycleService(
                _strategyExecutors, _consecutiveLossesMonitor, _orderRouter, _priceRounder, _logger, domainEventBus, _clock,
                _riskOrchestrator);

            // Flush inicial: deja el heartbeat.json creado con el estado al boot.
            // Se ejecuta tanto en backtest como en live.
            _heartbeatFileWriter.Flush();

            // Timer de wall clock para el flush periódico. Solo activo en live:
            // en backtest el heartbeat no tiene consumidor (Healthchecks.io alertaría
            // con 15 meses de "silencio" en cuestión de microsegundos del wall clock).
            //
            // Se usa System.Threading.Timer en lugar de Schedule.On porque:
            // 1) Schedule.On corre al ritmo del clock simulado del backtest (~650k disparos
            //    en 15 meses), no al ritmo del wall clock real que necesita el dead-man's switch.
            // 2) El heartbeat es observabilidad pasiva, no participa del flujo de trading,
            //    por lo que no requiere el determinismo del scheduler de QC.
            // 3) Trading.Strategies es el adaptador autorizado a usar primitivas de timing
            //    crudas; la regla del AI.md de "timers vía ITimer" aplica a Trading.Application.
            if (LiveMode)
            {
                _heartbeatFlushTimer = new System.Threading.Timer(
                    callback: _ =>
                    {
                        try
                        {
                            // Self-check del sink JSONL: escalar fallo a stderr → heartbeat.
                            var sinkFailure = _structuredLogSink.LastWriteFailure;
                            if (sinkFailure != null)
                                _healthHeartbeatTracker.RecordSinkWriteFailure(sinkFailure.Message);

                            _heartbeatFileWriter.Flush();
                            // Gatear el ping según frescura de barras: solo pingear mientras el
                            // algoritmo procesa barras activamente. Sin ping → Healthchecks.io DOWN.
                            // DateTime.UtcNow (wall clock real) per ADR-021: no IClock simulado.
                            if (_barStalenessGate.IsFresh(DateTime.UtcNow))
                                _ = _healthchecksPinger.PingAsync(System.Threading.CancellationToken.None);

                            // Auto-restart ante stall persistente del feed WebSocket: si no llega
                            // ninguna barra en >20 min (supera el período 15m de BTCUSDT, barra más
                            // frecuente), la re-suscripción post-reconexión falló silenciosamente.
                            // Exit(1) + NSSM → arranque limpio con socket nuevo.
                            // null = aún en warm-up; no se toca.
                            const double feedStallAutoRestartSeconds = 20 * 60;
                            var feedSnapshot = _healthHeartbeatTracker.Snapshot();
                            if (feedSnapshot.LastBarProcessedUtc.HasValue)
                            {
                                var staleness = (DateTime.UtcNow - feedSnapshot.LastBarProcessedUtc.Value).TotalSeconds;
                                if (staleness > feedStallAutoRestartSeconds)
                                {
                                    _logger.Warning(
                                        "Auto-restart: feed congelado {StalenessSeconds}s sin barra " +
                                        "(umbral {ThresholdSeconds}s). Terminando proceso — NSSM reconecta con socket limpio.",
                                        (int)staleness, (int)feedStallAutoRestartSeconds);
                                    Environment.Exit(1);
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            // Defensa en profundidad: el writer ya garantiza no propagar,
                            // pero en thread de Timer una excepción no manejada terminaría
                            // el proceso. Loggear y continuar.
                            _logger.Warning(
                                "Heartbeat flush timer falló: {ExceptionType} {Message}",
                                ex.GetType().Name, ex.Message);
                        }
                    },
                    state: null,
                    dueTime: System.TimeSpan.FromSeconds(60),
                    period: System.TimeSpan.FromSeconds(60));

                _logger.Info("Heartbeat flush timer iniciado (cadencia: 60s wall clock).");
            }
            else
            {
                _logger.Info(
                    "Heartbeat flush timer deshabilitado (modo backtest). " +
                    "El archivo heartbeat.json refleja el estado al boot.");
            }

            // Warm-up dinámico: el mayor entre el mínimo del HMM (100 barras × 4h ≈ 17 días)
            // y el requerimiento de cada estrategia (WarmUpBars × timeframe). Así cualquier
            // estrategia nueva con indicadores largos nunca arranca con historia parcial.
            const double hmmMinHours = 100.0 * 4.0; // 100 barras × 4h
            var warmUpSpan = TimeSpan.FromHours(hmmMinHours);
            foreach (var executor in _strategyExecutors)
            {
                var tfSpan = TimeframeHelper.GetTimeSpan(executor.Timeframe);
                var strategyWarmUp = TimeSpan.FromTicks(tfSpan.Ticks * executor.Strategy.WarmUpBars);
                if (strategyWarmUp > warmUpSpan)
                    warmUpSpan = strategyWarmUp;
            }
            SetWarmUp(warmUpSpan);
        }

        public override void OnWarmupFinished()
        {
            base.OnWarmupFinished();
            // El balance real del broker ya está cargado; este es el momento correcto
            // para fijar el high-water mark del DrawdownMonitor.
            _drawdownMonitor.InitializeWithCurrentValue();
            PlaceBrokerValidationOrderIfRequested();
        }

        // Hook D-prev: coloca UNA orden de ~$6 en SOLUSDT para verificar conectividad real del broker.
        // Activar: poner "broker-validation-mode": true en config.json (a nivel raíz).
        // Eliminar este método tras completar el checklist de Hito D-prev (ADR-041).
        //
        // Filtros reales Binance USDT-M SOLUSDT (consultados 2026-06-12):
        //   LOT_SIZE  minQty=0.01  stepSize=0.01  (quantityPrecision=2)
        //   MIN_NOTIONAL  $5
        private void PlaceBrokerValidationOrderIfRequested()
        {
            if (!LiveMode) return;
            if (!QuantConnect.Configuration.Config.GetBool("broker-validation-mode")) return;

            const decimal targetNotional = 6m;
            const decimal minNotional    = 5m;    // MIN_NOTIONAL Binance USDT-M SOLUSDT
            const decimal stepSize       = 0.01m; // LOT_SIZE stepSize SOLUSDT

            var instrumentId = new InstrumentId("SOLUSDT");
            var symbol       = _instrumentResolver.Resolve(instrumentId);
            decimal price    = Securities[symbol].Price;

            if (price <= 0)
            {
                _logger.Warning(
                    "D-prev — broker-validation-mode: precio SOLUSDT no disponible, abortando orden de prueba.");
                return;
            }

            // Ceil al step más cercano garantiza que el notional no caiga bajo el target
            decimal rawQty  = targetNotional / price;
            decimal quantity = Math.Ceiling(rawQty / stepSize) * stepSize;

            // Doble check contra el MIN_NOTIONAL del exchange
            if (quantity * price < minNotional)
                quantity = Math.Ceiling(minNotional / price / stepSize) * stepSize;

            _logger.Warning(
                "D-prev — broker-validation-mode activo: colocando orden de prueba {Quantity} SOL (~${Notional:F2} USDT) " +
                "en Binance USDT-M. Verificar fill en Binance Web y cerrar posición manualmente si SL/TP no la cierra. " +
                "Poner broker-validation-mode: false en config.json tras confirmar Hito D-prev completado.",
                quantity, quantity * price);

            _orderRouter.SubmitMarketOrder(instrumentId, quantity, OrderPurpose.Entry, "broker-validation-d-prev");
        }

        public override void OnData(Slice data)
        {
            if (IsWarmingUp) return;

            _riskOrchestrator.EvaluateAllMonitors();
            if (_riskOrchestrator.IsKillSwitchActivated) return;
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            var lifecycleEvent = OrderEventMapper.ToLifecycleEvent(
                orderEvent, this, _instrumentResolver, _orderRegistry, _logger);
            if (lifecycleEvent == null) return;
            _orderLifecycleService.Handle(lifecycleEvent);
        }

        public override void OnEndOfAlgorithm()
        {
            base.OnEndOfAlgorithm();

            if (_heartbeatFlushTimer != null)
            {
                _heartbeatFlushTimer.Dispose();
                _heartbeatFlushTimer = null;
            }

            _structuredLogSink?.Dispose();
            _httpClient?.Dispose();
        }

        private static IReadOnlySet<InstrumentId> ExtractInstrumentsRequiringRegime(
            Trading.Domain.Models.RootConfig rootConfiguration)
        {
            var instruments = new HashSet<InstrumentId>();
            foreach (var timeframeNode in rootConfiguration.Timeframes)
            {
                foreach (var strategyDefinition in timeframeNode.Value.Strategies)
                {
                    if (strategyDefinition.CompatibleRegimes != null &&
                        strategyDefinition.CompatibleRegimes.Count > 0)
                    {
                        instruments.Add(new InstrumentId(strategyDefinition.Symbol));
                    }
                }
            }
            return instruments;
        }

        private static IReadOnlyList<InstrumentId> ExtractActiveInstruments(
            Trading.Domain.Models.RootConfig rootConfiguration)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var instruments = new List<InstrumentId>();
            foreach (var timeframeNode in rootConfiguration.Timeframes)
            {
                foreach (var strategyDefinition in timeframeNode.Value.Strategies)
                {
                    if (seen.Add(strategyDefinition.Symbol))
                        instruments.Add(new InstrumentId(strategyDefinition.Symbol));
                }
            }
            return instruments;
        }
    }
}
