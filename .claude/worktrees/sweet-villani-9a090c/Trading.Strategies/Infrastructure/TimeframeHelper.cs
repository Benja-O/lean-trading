using System;

namespace Trading.Strategies.Infrastructure
{
    public static class TimeframeHelper
    {
        public static TimeSpan GetTimeSpan(string timeframeKey)
        {
            return timeframeKey.ToLower() switch
            {
                "1m" => TimeSpan.FromMinutes(1),
                "5m" => TimeSpan.FromMinutes(5),
                "15m" => TimeSpan.FromMinutes(15),
                "30m" => TimeSpan.FromMinutes(30),
                "1h" => TimeSpan.FromHours(1),
                "4h" => TimeSpan.FromHours(4),
                "1d" => TimeSpan.FromDays(1),
                _ => throw new ArgumentException($"Timeframe no soportado: {timeframeKey}")
            };
        }
    }
}
