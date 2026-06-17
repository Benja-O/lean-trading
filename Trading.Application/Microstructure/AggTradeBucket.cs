using System;

namespace Trading.Application.Microstructure
{
    /// <summary>
    /// Acumula aggTrades de Binance dentro de una ventana de 1h para el cómputo de features
    /// microestructurales. Replica la lógica de agrupación de _agg_1h() en download_aggtrades.py.
    ///
    /// Clasificación de lado:
    ///   isBuyerMaker = false → buyer agresivo (taker) → buy_volume
    ///   isBuyerMaker = true  → seller agresivo (taker) → sell_volume
    /// </summary>
    public sealed class AggTradeBucket
    {
        private decimal _firstPrice;
        private decimal _lastPrice;
        private decimal _highPrice = decimal.MinValue;
        private decimal _lowPrice  = decimal.MaxValue;
        private double _buyVolume;
        private double _sellVolume;
        private long _tradeCount;
        private double _sumQty;
        private bool _hasData;

        public bool HasData => _hasData;

        public double Open  => (double)_firstPrice;
        public double High  => (double)_highPrice;
        public double Low   => (double)_lowPrice;
        public double Close => (double)_lastPrice;

        public double BuyVolume  => _buyVolume;
        public double SellVolume => _sellVolume;
        public long   TradeCount => _tradeCount;

        /// <summary>mean(qty) = sum(qty) / count. Replica g["qty"].mean() del Python.</summary>
        public double MeanTradeSize => _tradeCount > 0 ? _sumQty / _tradeCount : 0.0;

        public void Add(decimal price, decimal qty, bool isBuyerMaker)
        {
            if (qty <= 0) return;

            double qtyD = (double)qty;

            if (!_hasData)
            {
                _firstPrice = price;
                _hasData    = true;
            }

            _lastPrice = price;

            if (price > _highPrice) _highPrice = price;
            if (price < _lowPrice)  _lowPrice  = price;

            if (isBuyerMaker)
                _sellVolume += qtyD;
            else
                _buyVolume  += qtyD;

            _sumQty += qtyD;
            _tradeCount++;
        }
    }
}
