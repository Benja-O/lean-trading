using System.Collections.Generic;

namespace Trading.Domain.Models
{
    public class TimeframeNode
    {
        public List<StrategyDefinition> Strategies { get; set; } = new();
    }
}
