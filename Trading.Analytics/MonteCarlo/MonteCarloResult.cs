namespace Trading.Analytics.MonteCarlo;

public sealed record MonteCarloResult(
    int Simulations,
    double SharpeP5,
    double SharpeP50,
    double SharpeP95,
    double MaxDDP50,
    double MaxDDP95,
    double CagrP5,
    double CagrP50,
    double CagrP95,
    double ProbabilityNegativeSharpe,
    double ProbabilityNegativeCagr)
{
    /// <summary>Resultado vacío cuando hay pocos trades para simular.</summary>
    public static readonly MonteCarloResult Insufficient =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 1.0, 1.0);
}
