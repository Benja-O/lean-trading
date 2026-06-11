using System.Globalization;

namespace Trading.Analytics.Parsing;

public static class LeanTransactionLogParser
{
    public static List<TradeFill> Parse(string filePath)
    {
        var fills = new List<TradeFill>();

        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 5) continue;

            var timestamp = DateTime.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
            var symbol = parts[1].Trim();
            var isBuy = parts[2].Trim().Equals("Buy", StringComparison.OrdinalIgnoreCase);
            var quantity = Math.Abs(decimal.Parse(parts[3].Trim(), CultureInfo.InvariantCulture));
            var price = decimal.Parse(parts[4].Trim(), CultureInfo.InvariantCulture);

            fills.Add(new TradeFill(timestamp, symbol, isBuy, quantity, price));
        }

        return fills.OrderBy(f => f.Timestamp).ToList();
    }
}
