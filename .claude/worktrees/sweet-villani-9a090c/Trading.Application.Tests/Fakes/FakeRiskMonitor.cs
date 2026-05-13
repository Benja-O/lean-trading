using Trading.Domain.Abstractions;

namespace Trading.Application.Tests.Fakes
{
    public class FakeRiskMonitor : IRiskMonitor
    {
        public string MonitorName { get; }
        public RiskAssessment NextAssessment { get; set; } = RiskAssessment.Pass();
        public int EvaluateCallCount { get; private set; }
        public int ResetCallCount { get; private set; }

        public FakeRiskMonitor(string monitorName = "FakeMonitor")
        {
            MonitorName = monitorName;
        }

        public RiskAssessment Evaluate()
        {
            EvaluateCallCount++;
            return NextAssessment;
        }

        public void Reset()
        {
            ResetCallCount++;
            NextAssessment = RiskAssessment.Pass();
        }
    }

    public class FakeRiskAction : IRiskAction
    {
        public int ExecuteCallCount { get; private set; }

        public void Execute()
        {
            ExecuteCallCount++;
        }
    }
}
