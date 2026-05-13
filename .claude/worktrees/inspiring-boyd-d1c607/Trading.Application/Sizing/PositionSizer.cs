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
    ///
    /// Retorna Result&lt;T, SizingFailureReason&gt; para flujos esperados que pueden fallar
    /// (precio inválido, cantidad rounding a cero, notional bajo el mínimo). El caller
    /// inspecciona FailureReason para decisiones; FailureDescription es para logs.
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
        /// Calcula la cantidad a operar según el riesgo configurado.
        /// Fórmula: quantity = (portfolioValue * riskPerTradeFraction) / (price * stopLossFraction)
        /// Redondea hacia abajo al lot size del instrumento.
        ///
        /// Devuelve Result.Failure si:
        /// - El precio es cero o negativo (InvalidPrice).
        /// - La cantidad redondeada al lot size resulta en cero (QuantityRoundsToZero).
        /// </summary>
        public Result<decimal, SizingFailureReason> CalculateQuantity(
            StrategyExecutor strategyExecutor, decimal price)
        {
            if (price <= 0m)
            {
                _logger.Error(
                    "PositionSizer: precio inválido ({Price}) para {InstrumentId}. Cálculo abortado.",
                    price, strategyExecutor.InstrumentId);
                return Result<decimal, SizingFailureReason>.Failure(
                    SizingFailureReason.InvalidPrice,
                    $"Precio recibido: {price}");
            }

            var riskParameters = strategyExecutor.RiskParameters;
            decimal portfolioValue = _portfolioState.TotalPortfolioValue;
            decimal riskAmount = portfolioValue * riskParameters.RiskPerTradeFraction;
            decimal stopDistancePerUnit = price * riskParameters.StopLossFraction;
            decimal rawQuantity = riskAmount / stopDistancePerUnit;

            decimal lotSize = _instrumentMetadata.GetLotSize(strategyExecutor.InstrumentId);
            decimal roundedQuantity = Math.Floor(rawQuantity / lotSize) * lotSize;

            if (roundedQuantity == 0m)
            {
                return Result<decimal, SizingFailureReason>.Failure(
                    SizingFailureReason.QuantityRoundsToZero,
                    $"Raw={rawQuantity}, LotSize={lotSize}, Rounded=0");
            }

            return Result<decimal, SizingFailureReason>.Success(roundedQuantity);
        }

        /// <summary>
        /// Valida que el notional de la orden propuesta supere el mínimo del exchange.
        ///
        /// Devuelve Result.Failure(BelowMinimumNotional, ...) si no lo supera.
        /// Si el exchange no declara mínimo, se usa 5 (unidad de la moneda de cuenta) como floor defensivo.
        /// </summary>
        public Result<SizingFailureReason> ValidateNotional(
            InstrumentId instrumentId, decimal quantity, decimal price)
        {
            decimal notionalValue = Math.Abs(quantity * price);
            decimal? minimumNotional = _instrumentMetadata.GetMinimumNotional(instrumentId);
            decimal floor = minimumNotional ?? 5m;

            if (notionalValue <= floor)
            {
                return Result<SizingFailureReason>.Failure(
                    SizingFailureReason.BelowMinimumNotional,
                    $"Notional={notionalValue}, Minimum={floor}");
            }

            return Result<SizingFailureReason>.Success();
        }
    }
}
