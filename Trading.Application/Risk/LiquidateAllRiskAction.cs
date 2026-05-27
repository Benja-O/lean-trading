using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Acción de mitigación de riesgo que liquida las posiciones de todos los
    /// instrumentos activos del sistema, uno por uno, manteniendo la disciplina
    /// de tags vía OrderRegistry.
    ///
    /// NO usa una "liquidación global" del broker porque eso genera órdenes
    /// con tags que el sistema no registra, lo que rompe la invariante de
    /// OrderEventMapper y deja StrategyHealthMonitor desincronizado (causa raíz
    /// de OPS-2 violado tras cooling-off, ver ADR-028).
    ///
    /// La lista de instrumentos activos se inyecta en el wiring; cada llamada
    /// a Execute itera sobre ella, consulta IPortfolioState.IsInvested, y solo
    /// emite LiquidateInstrument para los que tienen posición abierta.
    /// </summary>
    public sealed class LiquidateAllRiskAction : IRiskAction
    {
        private const string KillSwitchExecutorIdentifier = "RiskOrchestrator_KillSwitch";

        private readonly IOrderRouter _orderRouter;
        private readonly IPortfolioState _portfolioState;
        private readonly IReadOnlyList<InstrumentId> _activeInstruments;

        public LiquidateAllRiskAction(
            IOrderRouter orderRouter,
            IPortfolioState portfolioState,
            IReadOnlyList<InstrumentId> activeInstruments)
        {
            _orderRouter = orderRouter ?? throw new ArgumentNullException(nameof(orderRouter));
            _portfolioState = portfolioState ?? throw new ArgumentNullException(nameof(portfolioState));
            _activeInstruments = activeInstruments ?? throw new ArgumentNullException(nameof(activeInstruments));
        }

        public void Execute()
        {
            foreach (var instrumentId in _activeInstruments)
            {
                if (_portfolioState.IsInvested(instrumentId))
                {
                    _orderRouter.LiquidateInstrument(
                        instrumentId,
                        OrderPurpose.Liquidate,
                        KillSwitchExecutorIdentifier);
                }
            }
        }
    }
}
