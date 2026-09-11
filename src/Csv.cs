using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

static class Csv
{
    public static Row[] Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 20000)
            throw new ArgumentException("Provide a CSV file under 20 KB.");
        var records = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, closed = false;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                        closed = true;
                    }
                }
                else
                    field.Append(c);
                continue;
            }

            if (c == '"')
            {
                if (field.Length != 0 || closed)
                    throw new ArgumentException("Unexpected CSV quote.");
                quoted = true;
            }
            else if (c == ',' || c == '\n' || c == '\r')
            {
                fields.Add(field.ToString());
                field.Clear();
                closed = false;
                if (c != ',')
                {
                    records.Add(fields);
                    fields = [];
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                }
            }
            else
            {
                if (closed)
                    throw new ArgumentException("Unexpected text after CSV quote.");
                field.Append(c);
            }
        }

        if (quoted)
            throw new ArgumentException("Unclosed CSV quote.");
        if (field.Length > 0 || fields.Count > 0 || closed)
        {
            fields.Add(field.ToString());
            records.Add(fields);
        }

        records = records.Where(r => r.Any(x => x.Length > 0)).ToList();
        if (records.Count < 2 || !records[0].Select(x => x.Trim().ToLowerInvariant()).SequenceEqual(new[] { "id", "currency", "amount" }))
            throw new ArgumentException("CSV header must be id,currency,amount.");
        if (records.Count > 101)
            throw new ArgumentException("Import up to 100 rows.");
        var rows = new List<Row>();
        foreach (var record in records.Skip(1))
        {
            if (record.Count != 3)
                throw new ArgumentException("Each CSV row requires three fields.");
            var id = record[0].Trim();
            var currency = record[1].Trim().ToUpperInvariant();
            if (id.Length is < 1 or > 80 || !Rates.Currencies.Contains(currency) || !decimal.TryParse(record[2], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || Math.Abs(amount) > 1_000_000_000 || decimal.Round(amount, 4) != amount)
                throw new ArgumentException("Invalid row: use a unique ID, supported currency and decimal amount up to 1 billion with at most 4 decimals.");
            if (rows.Any(x => x.Id == id))
                throw new ArgumentException("Duplicate row ID: " + id);
            rows.Add(new(id, currency, amount));
        }

        return rows.ToArray();
    }
}
