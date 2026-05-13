using System;

namespace Trading.Domain.Models
{
    public class MarketData
    {
        public string Symbol { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public DateTime Time { get; set; }
    }
}
