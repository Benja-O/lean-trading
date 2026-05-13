using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Slippage;
using Trading.Application.Execution;
using Trading.Application.Risk;
using Trading.Application.Sizing;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Adapters;
using Trading.Strategies.Infrastructure;

namespace Trading.Strategies
{
    /// <summary>
    /// Host del sistema. Es el ÚNICO lugar que extiende QCAlgorithm y compone los adaptadores Lean
    /// con los servicios de Trading.Application.
    /// 
    /// Responsabilidades:
    /// 1. Configuración del backtest/live (fechas, cash, brokerage, símbolos).
    /// 2. Construcción de adaptadores Lean (resolver, portfolio, metadata, router, clock, logger).
    /// 3. Construcción de servicios de Application (KillSwitch, Sizer, BarProcessing, OrderLifecycle).
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

        // Servicios de Application
        private OrderRegistry _orderRegistry;
        private KillSwitchManager _killSwitchManager;
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
            _logger = new LeanLogger(this);
            _priceRounder = new PriceRounder(_instrumentMetadata);

            // ===== Servicios de Application =====
            _killSwitchManager = new KillSwitchManager(_portfolioState, _orderRouter, _clock, _logger);
            _positionSizer = new PositionSizer(_portfolioState, _instrumentMetadata, _logger);

            // ===== Carga y validación de configuración =====
            // El loader falla loud si RiskPerTradePercentage no está presente o es inválido en cualquier definición.
            string strategiesFilePath = @"F:\DesarrolloTrading\QuantConnect\Lean\Trading.Strategies\bin\Debug\net10.0\strategies.json";
            var rootConfiguration = _strategyConfigurationLoader.Load(strategiesFilePath);

            // ===== Configuración del entorno de trading =====
            SetStartDate(2025, 1, 1);
            SetEndDate(2026, 3, 31);
            SetAccountCurrency("USDT");
            SetCash("USDT", 100000);
            SetCash("USD", 0);

            _killSwitchManager.InitializePortfolioValue();

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

            // ===== Construcción de executors =====
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

                        // Defensa en profundidad: el loader ya validó que .RiskPerTradePercentage tiene valor.
                        // Acceder con .Value acá es seguro y mantiene la política fail-loud si algo se rompe arriba.
                        var riskParameters = RiskParameters.FromPercentages(
                            stopLossPercentage: strategyDefinition.StopLossPercentage,
                            takeProfitPercentage: strategyDefinition.TakeProfitPercentage,
                            riskPerTradePercentage: strategyDefinition.RiskPerTradePercentage.Value);

                        var strategyExecutor = new StrategyExecutor(
                            strategyDefinition, timeframe, instrumentId, strategy, riskParameters);
                        _strategyExecutors.Add(strategyExecutor);
                        localStrategyExecutors.Add(strategyExecutor);
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

            // ===== Servicios que requieren la lista de executors ya armada =====
            _barProcessingService = new BarProcessingService(
                _portfolioState, _orderRouter, _killSwitchManager, _positionSizer, _logger);

            _orderLifecycleService = new OrderLifecycleService(
                _strategyExecutors, _killSwitchManager, _orderRouter, _priceRounder, _logger);

            SetWarmUp(TimeSpan.FromDays(1));
        }

        public override void OnData(Slice data)
        {
            if (IsWarmingUp) return;

            _killSwitchManager.EvaluateCoolingOffPeriod();
            if (_killSwitchManager.IsKillSwitchActivated) return;

            _killSwitchManager.CheckDrawdownKillSwitch();
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            var lifecycleEvent = OrderEventMapper.ToLifecycleEvent(
                orderEvent, this, _instrumentResolver, _orderRegistry, _logger);
            if (lifecycleEvent == null) return;
            _orderLifecycleService.Handle(lifecycleEvent);
        }
    }
}
