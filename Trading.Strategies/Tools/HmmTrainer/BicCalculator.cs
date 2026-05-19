using System;

namespace Trading.Strategies.Tools.HmmTrainer
{
    /// <summary>
    /// Cálculo del Bayesian Information Criterion (BIC) para selección de número de estados
    /// del HMM. Fórmula: <c>BIC = ln(N) · p − 2 · logL</c>, donde N es el número de observaciones,
    /// p el número de parámetros del modelo y logL la log-likelihood final del entrenamiento.
    ///
    /// Menor BIC = mejor compromiso entre ajuste y parsimonia. Penaliza modelos sobre-parametrizados.
    ///
    /// Parámetros de un HMM continuo con K estados y emisiones Multivariate Gaussian de dimensión D:
    /// - Vector inicial π: (K - 1) parámetros libres (suman 1).
    /// - Matriz de transición A: K · (K - 1) parámetros libres (cada fila suma 1).
    /// - Media de cada estado: K · D parámetros.
    /// - Covarianza simétrica de cada estado: K · D · (D + 1) / 2 parámetros.
    /// </summary>
    public static class BicCalculator
    {
        public static double Compute(int numberOfStates, int featureDimension, int observationCount, double finalLogLikelihood)
        {
            if (numberOfStates <= 0) throw new ArgumentOutOfRangeException(nameof(numberOfStates));
            if (featureDimension <= 0) throw new ArgumentOutOfRangeException(nameof(featureDimension));
            if (observationCount <= 0) throw new ArgumentOutOfRangeException(nameof(observationCount));

            int initialParameters = numberOfStates - 1;
            int transitionParameters = numberOfStates * (numberOfStates - 1);
            int meanParameters = numberOfStates * featureDimension;
            int covarianceParameters = numberOfStates * featureDimension * (featureDimension + 1) / 2;
            int totalParameters = initialParameters + transitionParameters + meanParameters + covarianceParameters;

            return Math.Log(observationCount) * totalParameters - 2.0 * finalLogLikelihood;
        }
    }
}
