using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

record Calculation(string Csv, string? ReportingCurrency, string Date, decimal ShockPercent, string RequestId);
record Row(string Id, string Currency, decimal Amount);
record Rate(string Date, string Base, string Quote, decimal Value, string Source);
record Converted(string Id, string Currency, decimal Amount, decimal Rate, string RateDate, decimal Baseline, decimal Scenario);
record Snapshot(string RequestId, string InputHash, DateTimeOffset CreatedAt, string RequestedDate, string ReportingCurrency, decimal ShockPercent, decimal BaselineTotal, decimal ScenarioTotal, Converted[] Rows, Rate[] Rates, string Rounding);
