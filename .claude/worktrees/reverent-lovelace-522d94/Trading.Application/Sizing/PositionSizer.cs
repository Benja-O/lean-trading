using System;
using Trading.Application.Execution;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Sizing
{
    /// <summary>
    /// Calcula tamaño de posición y valida notional mínimo.
    /// 
    /// Sin acoplamiento a QuantConnect: usa IPortfolioState e IInstrumentMetadata.
    /// El fallback silencioso fue eliminado en el refactor previo: si el sizer ejecuta,
    /// los RiskParameters están garantizados como válidos por construcción del value object.
    /// </summary>
    public class PositionSizer
    {
        private readonly IPortfolioState _portfolioState;
        private readonly IInstrumentMetadata _instrumentMetadata;
        private readonly ITradingLogger _logger;

        public PositionSizer(
            IPortfolioState portfolioState,
            IInstrumentMetadata instrumentMetadata,
            ITradingLogger logger)
        {
            _portfolioState = portfolioState ?? throw new ArgumentNullException(nameof(portfolioState));
            _instrumentMetadata = instrumentMetadata ?? throw new ArgumentNullException(nameof(instrumentMetadata));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Calcula la cantidad a operar según el riesgo configurado para esta estrategia.
        /// Fórmula: quantity = (portfolioValue * riskPerTradeFraction) / (price * stopLossFraction)
        /// El resultado se redondea hacia abajo al lot size del instrumento.
        /// </summary>
        public decimal CalculateQuantity(StrategyExecutor strategyExecutor, decimal price)
        {
            if (price <= 0m)
            {
                _logger.Error(
                    "PositionSizer: precio inválido ({Price}) para {InstrumentId}. Orden bloqueada.",
                    price, strategyExecutor.InstrumentId);
                return 0m;
            }

            var riskParameters = strategyExecutor.RiskParameters;
            decimal portfolioValue = _portfolioState.TotalPortfolioValue;
            decimal riskAmount = portfolioValue * riskParameters.RiskPerTradeFraction;
            decimal stopDistancePerUnit = price * riskParameters.StopLossFraction;
            decimal rawQuantity = riskAmount / stopDistancePerUnit;

            decimal lotSize = _instrumentMetadata.GetLotSize(strategyExecutor.InstrumentId);
            return Math.Floor(rawQuantity / lotSize) * lotSize;
        }

        public bool IsValidNotional(InstrumentId instrumentId, decimal quantity, decimal price)
        {
            decimal notionalValue = Math.Abs(quantity * price);
            decimal? minimumNotional = _instrumentMetadata.GetMinimumNotional(instrumentId);
            return notionalValue > (minimumNotional ?? 5m);
        }
    }
}
