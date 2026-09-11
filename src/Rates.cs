using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

sealed class Rates(HttpClient http, bool fixture)
{
    public static readonly HashSet<string> Currencies = "EUR USD GBP JPY MXN CAD CHF AUD".Split(' ').ToHashSet();
    public async Task<Rate> Get(string from, string to, DateOnly date)
    {
        if (fixture)
        {
            var values = new Dictionary<string, decimal>
            {
                {
                    "EUR",
                    1
                },
                {
                    "USD",
                    1.2m
                },
                {
                    "GBP",
                    .8m
                },
                {
                    "JPY",
                    150
                },
                {
                    "MXN",
                    20
                },
                {
                    "CAD",
                    1.5m
                },
                {
                    "CHF",
                    .9m
                },
                {
                    "AUD",
                    1.6m
                }
            };
            return new(date.ToString("yyyy-MM-dd"), from, to, values[to] / values[from], "test fixtures");
        }

        for (int back = 0; back < 8; back++)
        {
            var requested = date.AddDays(-back).ToString("yyyy-MM-dd");
            using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.frankfurter.dev/v2/rates?base={from}&quotes={to}&providers=ecb&date={requested}"));
            if (doc.RootElement.GetArrayLength() == 0)
                continue;
            var row = doc.RootElement[0];
            var value = row.GetProperty("rate").GetDecimal();
            var observed = DateOnly.Parse(row.GetProperty("date").GetString()!);
            if (value <= 0 || observed > date || date.DayNumber - observed.DayNumber > 7)
                throw new HttpRequestException("Invalid reference rate.");
            return new(observed.ToString("yyyy-MM-dd"), from, to, value, "Frankfurter / ECB daily reference rates");
        }

        throw new HttpRequestException("No rate within the previous seven days.");
    }
}
