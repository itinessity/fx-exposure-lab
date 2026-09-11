using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

var app = Setup.Create(args);
var rates = new Rates(Setup.Http(app), Setup.Fixture(app));
var store = new Store<Snapshot>("snapshots");
var gate = new SemaphoreSlim(1);
app.MapPost("/api/calculate", async (Calculation input) =>
{
    await gate.WaitAsync();
    try
    {
        if (input.RequestId is null)
            throw new ArgumentException("A request ID is required.");
        var rows = Csv.Parse(input.Csv);
        if (!Rates.Currencies.Contains(input.ReportingCurrency ?? ""))
            throw new ArgumentException("Unsupported reporting currency.");
        if (input.ShockPercent is < -50 or > 50)
            throw new ArgumentException("Scenario must be between -50% and +50%.");
        if (!DateOnly.TryParseExact(input.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date < DateOnly.Parse("1999-01-04") || date > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Choose a date between 1999-01-04 and today.");
        if (input.RequestId.Length is < 1 or > 80)
            throw new ArgumentException("A request ID of 1–80 characters is required.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input with { RequestId = "" }, Setup.Json))));
        foreach (var existing in store.List(200))
        {
            var saved = store.Get(existing)!;
            if (saved.RequestId == input.RequestId)
            {
                if (saved.InputHash != hash)
                    return Results.Conflict(new { error = "This request ID was already used with different inputs." });
                return Results.Ok(new { id = existing, snapshot = saved, reused = true });
            }
        }

        var collected = new Dictionary<string, Rate>();
        foreach (var currency in rows.Select(x => x.Currency).Distinct())
            collected[currency] = currency == input.ReportingCurrency ? new(date.ToString("yyyy-MM-dd"), currency, input.ReportingCurrency, 1, "Identity conversion") : await rates.Get(currency, input.ReportingCurrency!, date);
        var digits = input.ReportingCurrency == "JPY" ? 0 : 2;
        var converted = rows.Select(row =>
        {
            var rate = collected[row.Currency];
            var baseline = Math.Round(row.Amount * rate.Value, digits, MidpointRounding.ToEven);
            var shocked = rate.Value * (row.Currency == input.ReportingCurrency ? 1 : 1 + input.ShockPercent / 100);
            return new Converted(row.Id, row.Currency, row.Amount, rate.Value, rate.Date, baseline, Math.Round(row.Amount * shocked, digits, MidpointRounding.ToEven));
        }).ToArray();
        var snapshot = new Snapshot(input.RequestId, hash, DateTimeOffset.UtcNow, input.Date, input.ReportingCurrency!, input.ShockPercent, converted.Sum(x => x.Baseline), converted.Sum(x => x.Scenario), converted, collected.Values.ToArray(), "Per-line rounding: midpoint to even; JPY 0 decimals, other supported currencies 2.");
        var id = store.Save(snapshot);
        return Results.Ok(new { id, snapshot, reused = false });
    }
    finally
    {
        gate.Release();
    }
});
app.MapGet("/api/snapshots", () => store.List());
app.MapGet("/api/snapshots/{id}", (string id) => store.Get(id)is { } s ? Results.Ok(s) : Results.NotFound(new { error = "Snapshot not found." }));
app.Run();
