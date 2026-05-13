using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Tests.Fakes
{
    /// <summary>
    /// Monitor de riesgo falso para tests. Permite configurar el veredicto vía NextAssessment y
    /// verificar cuántas veces fue invocado Evaluate() y Reset().
    /// El NextAssessment se resetea a Pass() después de cada Evaluate() para simular un monitor
    /// que vuelve a pasar si no se configura de nuevo.
    /// </summary>
    internal sealed class FakeRiskMonitor : IRiskMonitor
    {
        public string MonitorName { get; }
        public int EvaluateCallCount { get; private set; }
        public int ResetCallCount { get; private set; }
        public RiskAssessment NextAssessment { get; set; } = RiskAssessment.Pass();

        public FakeRiskMonitor(string monitorName = "FakeMonitor")
        {
            MonitorName = monitorName;
        }

        public RiskAssessment Evaluate()
        {
            EvaluateCallCount++;
            var result = NextAssessment;
            NextAssessment = RiskAssessment.Pass();
            return result;
        }

        public void Reset()
        {
            ResetCallCount++;
        }
    }

    /// <summary>
    /// Acción de riesgo falsa para tests. Registra cuántas veces fue ejecutada.
    /// </summary>
    internal sealed class FakeRiskAction : IRiskAction
    {
        public int ExecuteCallCount { get; private set; }

        public void Execute()
        {
            ExecuteCallCount++;
        }
    }
}
