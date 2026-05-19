using System;
using System.Collections.Generic;

namespace Trading.Strategies.Regimes
{
    /// <summary>
    /// DTO de serialización del modelo HMM entrenado. Es el contrato JSON que se persiste en
    /// <c>models/regime/{instrument}-perp-binance.hmm.json</c> y que el factory de runtime
    /// vuelve a deserializar al arrancar.
    ///
    /// Contiene todo lo necesario para reconstruir el modelo en runtime SIN volver a entrenar:
    /// - Parámetros del HMM (π, A, μ por estado, Σ por estado).
    /// - Parámetros del scaler usados en entrenamiento (medias y desvíos), para que las features
    ///   de runtime se normalicen exactamente con los mismos parámetros.
    /// - Mapeo crudo → semántico calculado offline.
    /// - Metadata de auditoría: instrumento, ventana de entrenamiento, K elegido, BIC final.
    /// </summary>
    public sealed record PersistedHmmModel(
        string InstrumentIdentifier,
        string Exchange,
        string ContractType,
        string Timeframe,
        DateTime TrainedAtUtc,
        DateTime TrainingWindowStartUtc,
        DateTime TrainingWindowEndUtc,
        int NumberOfStates,
        double FinalBic,
        double[] InitialProbabilities,
        double[][] TransitionMatrix,
        double[][] EmissionMeans,
        double[][][] EmissionCovariances,
        double[] FeatureScalerMeans,
        double[] FeatureScalerStdDevs,
        Dictionary<int, string> StateToRegimeLabel,
        int WarmUpBars
    );
}
