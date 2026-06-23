using Trading.Domain.ValueObjects;

namespace Trading.Domain.Models
{
    /// <summary>
    /// Features microestructurales derivadas de AggTrades de Binance, agregadas a una barra 1h.
    /// Complementa a MarketBar (OHLCV) con información de flujo de órdenes que no es observable
    /// en datos de precio solamente.
    ///
    /// Convenciones de signo:
    ///   Ofi > 0  → presión compradora neta (agresores compradores > vendedores)
    ///   Ofi < 0  → presión vendedora neta
    ///   CvdDelta = buy_volume - sell_volume en la barra
    ///   Cvd      = suma acumulativa continua de CvdDelta desde el inicio del dataset
    /// </summary>
    public sealed class MicrostructureBar
    {
        public InstrumentId InstrumentId { get; }
        public DateTime BarUtc { get; }

        /// <summary>Order Flow Imbalance: (buy_vol - sell_vol) / total_vol. Rango [-1, 1].</summary>
        public double Ofi { get; }

        /// <summary>Variación de CVD en esta barra (buy_vol - sell_vol en unidades del activo).</summary>
        public double CvdDelta { get; }

        /// <summary>Cumulative Volume Delta acumulado desde el inicio del dataset.</summary>
        public double Cvd { get; }

        /// <summary>Cantidad de trades (aggTrades) en la barra. Proxy de urgencia del mercado.</summary>
        public double ArrivalRate { get; }

        /// <summary>Tamaño medio por trade en la barra. Distingue flujo institucional (grande) de retail (pequeño).</summary>
        public double MeanTradeSize { get; }

        /// <summary>Ratio buy_vol / sell_vol. Collinear con Ofi pero sin normalizar.</summary>
        public double BuySellRatio { get; }

        /// <summary>Retorno de precio en la barra: (close - open) / open.</summary>
        public double PriceReturn { get; }

        // OHLCV de la barra (precios → decimal por AI.md). Propiedades init opcionales: las
        // pobla el productor (MicrostructureFeatureComputer desde el bucket de aggTrades) cuando
        // el dato está disponible. Quedan en 0 cuando se cargan de un store viejo sin columnas
        // OHLCV. Permiten reconstruir un MarketBar para warmear estrategias desde el store sin
        // depender de history de precios del broker (que en live-tick no existe).
        /// <summary>Precio de apertura de la barra. 0 si el origen no lo provee.</summary>
        public decimal Open { get; init; }
        /// <summary>Precio máximo de la barra. 0 si el origen no lo provee.</summary>
        public decimal High { get; init; }
        /// <summary>Precio mínimo de la barra. 0 si el origen no lo provee.</summary>
        public decimal Low { get; init; }
        /// <summary>Precio de cierre de la barra. 0 si el origen no lo provee.</summary>
        public decimal Close { get; init; }
        /// <summary>Volumen total (base) de la barra. 0 si el origen no lo provee.</summary>
        public decimal Volume { get; init; }

        public MicrostructureBar(
            InstrumentId instrumentId,
            DateTime barUtc,
            double ofi,
            double cvdDelta,
            double cvd,
            double arrivalRate,
            double meanTradeSize,
            double buySellRatio,
            double priceReturn)
        {
            InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
            BarUtc       = barUtc;
            Ofi          = ofi;
            CvdDelta     = cvdDelta;
            Cvd          = cvd;
            ArrivalRate  = arrivalRate;
            MeanTradeSize = meanTradeSize;
            BuySellRatio  = buySellRatio;
            PriceReturn   = priceReturn;
        }
    }
}
