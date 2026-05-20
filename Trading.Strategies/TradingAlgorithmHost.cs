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

        // Servicios de Application
        private OrderRegistry _orderRegistry;
        private RiskOrchestrator _riskOrchestrator;
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
            _orderRouter = new LeanOrderRouter(this, _instrumentResolver, _orderRegistry);
            _clock = new LeanClock(this);
            // logs/trading-{fecha}.jsonl relativo al directorio de salida, retención 30 días
            _structuredLogSink = new JsonlFileLogSink(_clock);
            _logger = new LeanLogger(this, _structuredLogSink);
            _priceRounder = new PriceRounder(_instrumentMetadata);

            // ===== Servicios de Application =====
            var domainEventBus = new DomainEventBus(_logger);

            // Heartbeat: suscripto a eventos de dominio antes de que se emitan
            _healthHeartbeatTracker = new HealthHeartbeatTracker(domainEventBus, _clock, _logger);
            _heartbeatFileWriter = new HeartbeatFileWriter(_healthHeartbeatTracker, _clock, _logger);
            var pingUrl = Environment.GetEnvironmentVariable("HEALTHCHECKS_PING_URL");
            _httpClient = new HttpClient();
            _healthchecksPinger = new HealthchecksIoPinger(pingUrl, _httpClient, _clock, _logger);

            var drawdownMonitor = new DrawdownMonitor(_portfolioState, 0.25m);
            _consecutiveLossesMonitor = new ConsecutiveLossesMonitor(8);
            var riskAction = new LiquidateAllRiskAction(_orderRouter);
            var coolingOffTracker = new CoolingOffTracker(_clock, TimeSpan.FromDays(1));
            _riskOrchestrator = new RiskOrchestrator(
                new IRiskMonitor[] { drawdownMonitor, _consecutiveLossesMonitor },
                riskAction, coolingOffTracker, _clock, _logger, domainEventBus);
            _positionSizer = new PositionSizer(_portfolioState, _instrumentMetadata, _logger);

            // ===== Carga y validación de configuración =====
            string strategiesFilePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "strategies.json");
            var rootConfiguration = _strategyConfigurationLoader.Load(strategiesFilePath);

            // ===== Configuración del entorno de trading =====
            SetStartDate(2025, 1, 1);
            SetEndDate(2026, 3, 31);
            SetAccountCurrency("USDT");
            SetCash("USDT", 100000);
            SetCash("USD", 0);

            drawdownMonitor.InitializeWithCurrentValue();

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
                cryptoAsset.SetFeeModel(new ConstantFeeModel(0.001m));
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
                        var strategy = StrategyFactory.Create(strategyDefinition.StrategyName);

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
                        if (IsWarmingUp) return;
                        var marketBar = MarketBarMapper.ToMarketBar(
                            (TradeBar)tradeBarData, _instrumentResolver);
                        _barProcessingService.ProcessBar(marketBar, localStrategyExecutors);
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
            _barProcessingService = new BarProcessingService(
                _portfolioState, _orderRouter, _riskOrchestrator, _positionSizer,
                _logger, domainEventBus, _clock,
                regimeRegistry, strategyCompatibilities);

            _orderLifecycleService = new OrderLifecycleService(
                _strategyExecutors, _consecutiveLossesMonitor, _orderRouter, _priceRounder, _logger, domainEventBus, _clock);

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
                            _heartbeatFileWriter.Flush();
                            // fire-and-forget deliberado: el callback del Timer es síncrono;
                            // el pinger garantiza no propagar excepciones.
                            _ = _healthchecksPinger.PingAsync(System.Threading.CancellationToken.None);
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

            // 20 días de calendario cubren las 100 barras 4h de warm-up del HMM con margen
            // (17 días serían el mínimo estricto: 100 · 4h = 16.67 días).
            SetWarmUp(TimeSpan.FromDays(20));
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
    }
}
