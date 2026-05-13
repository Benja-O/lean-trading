using System;
using System.Linq;
using Trading.Application.Risk;
using Trading.Application.Tests.Fakes;
using Xunit;

namespace Trading.Application.Tests.Risk
{
    /// <summary>
    /// Tests del KillSwitchManager. Posibles ahora porque la lógica vive en Application
    /// y depende solo de abstracciones del dominio. Cero referencia a QuantConnect.
    /// 
    /// Cobertura:
    /// - Drawdown: activa al cruzar el umbral, no antes.
    /// - Pérdidas consecutivas: activa al alcanzar el límite, no antes.
    /// - Reset de pérdidas: tras una victoria, vuelve a empezar.
    /// - Cooling-off: el sistema sale del kill switch sólo después del período configurado.
    /// - Liquidación al activar: confirma que se llamó LiquidateAll exactamente una vez.
    /// </summary>
    public class KillSwitchManagerTests
    {
        private readonly FakePortfolioState _portfolioState = new();
        private readonly FakeOrderRouter _orderRouter = new();
        private readonly FakeClock _clock = new();
        private readonly FakeTradingLogger _logger = new();

        private KillSwitchManager BuildKillSwitchManager(
            decimal drawdownLimit = 0.25m,
            int consecutiveLossesLimit = 8,
            TimeSpan? coolingOffPeriod = null)
        {
            return new KillSwitchManager(
                _portfolioState, _orderRouter, _clock, _logger,
                maximumDrawdownFraction: drawdownLimit,
                maximumConsecutiveLosses: consecutiveLossesLimit,
                coolingOffPeriod: coolingOffPeriod ?? TimeSpan.FromDays(1));
        }

        // ===== Drawdown =====

        [Fact]
        public void CheckDrawdown_BelowLimit_DoesNotActivate()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var killSwitch = BuildKillSwitchManager();
            killSwitch.InitializePortfolioValue();

            _portfolioState.TotalPortfolioValue = 80_000m; // 20% drawdown, límite 25%
            killSwitch.CheckDrawdownKillSwitch();

            Assert.False(killSwitch.IsKillSwitchActivated);
            Assert.Equal(0, _orderRouter.LiquidateAllCallCount);
        }

        [Fact]
        public void CheckDrawdown_AtLimit_Activates()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var killSwitch = BuildKillSwitchManager(drawdownLimit: 0.25m);
            killSwitch.InitializePortfolioValue();

            _portfolioState.TotalPortfolioValue = 75_000m; // 25% exacto
            killSwitch.CheckDrawdownKillSwitch();

            Assert.True(killSwitch.IsKillSwitchActivated);
            Assert.Equal(1, _orderRouter.LiquidateAllCallCount);
        }

        [Fact]
        public void CheckDrawdown_UpdatesPeakOnNewHigh()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var killSwitch = BuildKillSwitchManager(drawdownLimit: 0.25m);
            killSwitch.InitializePortfolioValue();

            // Subimos a un nuevo máximo
            _portfolioState.TotalPortfolioValue = 120_000m;
            killSwitch.CheckDrawdownKillSwitch();
            Assert.False(killSwitch.IsKillSwitchActivated);

            // Caída de 20% sobre 120k = 96k → no activa
            _portfolioState.TotalPortfolioValue = 96_000m;
            killSwitch.CheckDrawdownKillSwitch();
            Assert.False(killSwitch.IsKillSwitchActivated);

            // Caída de 25% sobre 120k = 90k → activa
            _portfolioState.TotalPortfolioValue = 90_000m;
            killSwitch.CheckDrawdownKillSwitch();
            Assert.True(killSwitch.IsKillSwitchActivated);
        }

        // ===== Pérdidas consecutivas =====

        [Fact]
        public void RegisterLoss_BelowLimit_DoesNotActivate()
        {
            var killSwitch = BuildKillSwitchManager(consecutiveLossesLimit: 8);

            for (int lossIndex = 0; lossIndex < 7; lossIndex++)
            {
                killSwitch.RegisterLoss();
            }

            Assert.False(killSwitch.IsKillSwitchActivated);
        }

        [Fact]
        public void RegisterLoss_AtLimit_Activates()
        {
            var killSwitch = BuildKillSwitchManager(consecutiveLossesLimit: 8);

            for (int lossIndex = 0; lossIndex < 8; lossIndex++)
            {
                killSwitch.RegisterLoss();
            }

            Assert.True(killSwitch.IsKillSwitchActivated);
            Assert.Equal(1, _orderRouter.LiquidateAllCallCount);
        }

        [Fact]
        public void ResetLossCounter_AfterPartialLosses_PreventsActivation()
        {
            var killSwitch = BuildKillSwitchManager(consecutiveLossesLimit: 8);

            for (int lossIndex = 0; lossIndex < 7; lossIndex++)
            {
                killSwitch.RegisterLoss();
            }

            killSwitch.ResetLossCounter();

            for (int lossIndex = 0; lossIndex < 7; lossIndex++)
            {
                killSwitch.RegisterLoss();
            }

            Assert.False(killSwitch.IsKillSwitchActivated);
        }

        // ===== Cooling-off =====

        [Fact]
        public void EvaluateCoolingOff_BeforePeriod_StaysActive()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var killSwitch = BuildKillSwitchManager(
                drawdownLimit: 0.25m,
                coolingOffPeriod: TimeSpan.FromDays(1));
            killSwitch.InitializePortfolioValue();

            _portfolioState.TotalPortfolioValue = 70_000m;
            killSwitch.CheckDrawdownKillSwitch();
            Assert.True(killSwitch.IsKillSwitchActivated);

            _clock.Advance(TimeSpan.FromHours(23));
            killSwitch.EvaluateCoolingOffPeriod();

            Assert.True(killSwitch.IsKillSwitchActivated);
        }

        [Fact]
        public void EvaluateCoolingOff_AfterPeriod_Deactivates()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var killSwitch = BuildKillSwitchManager(
                drawdownLimit: 0.25m,
                coolingOffPeriod: TimeSpan.FromDays(1));
            killSwitch.InitializePortfolioValue();

            _portfolioState.TotalPortfolioValue = 70_000m;
            killSwitch.CheckDrawdownKillSwitch();

            _portfolioState.TotalPortfolioValue = 75_000m; // recupera algo durante cooling
            _clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));
            killSwitch.EvaluateCoolingOffPeriod();

            Assert.False(killSwitch.IsKillSwitchActivated);
        }

        [Fact]
        public void EvaluateCoolingOff_AfterDeactivation_ResetsLossCounter()
        {
            var killSwitch = BuildKillSwitchManager(
                consecutiveLossesLimit: 3,
                coolingOffPeriod: TimeSpan.FromDays(1));

            for (int lossIndex = 0; lossIndex < 3; lossIndex++) killSwitch.RegisterLoss();
            Assert.True(killSwitch.IsKillSwitchActivated);

            _portfolioState.TotalPortfolioValue = 100_000m;
            _clock.Advance(TimeSpan.FromDays(2));
            killSwitch.EvaluateCoolingOffPeriod();

            Assert.False(killSwitch.IsKillSwitchActivated);

            // Tras deactivation, dos pérdidas más NO deben re-activar (counter reseteado).
            killSwitch.RegisterLoss();
            killSwitch.RegisterLoss();
            Assert.False(killSwitch.IsKillSwitchActivated);
        }

        // ===== Activación explícita =====

        [Fact]
        public void ActivateKillSwitch_LiquidatesAndLogsError()
        {
            var killSwitch = BuildKillSwitchManager();

            killSwitch.ActivateKillSwitch("test reason");

            Assert.True(killSwitch.IsKillSwitchActivated);
            Assert.Equal(1, _orderRouter.LiquidateAllCallCount);
            Assert.Single(_logger.CriticalEntries);
            Assert.Contains("test reason", _logger.CriticalEntries[0].Arguments.Cast<object>().Select(arg => arg?.ToString()));
        }
    }
}
