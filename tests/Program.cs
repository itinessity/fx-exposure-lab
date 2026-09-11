static void Check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}

var rows = Csv.Parse("id,currency,amount\r\n\"invoice, one\",USD,1.2345\r\n\"quote \"\"two\"\"\",EUR,-4\r\n");
Check(rows.Length == 2 && rows[0].Id == "invoice, one" && rows[1].Id == "quote \"two\"", "quoted CSV fields");
Check(rows[0].Amount == 1.2345m && rows[1].Amount == -4, "invariant decimal parsing");
foreach (var bad in new[]
{
    "id,currency,amount\na,USD,1\na,EUR,2",
    "id,currency,amount\na,XXX,1",
    "id,currency,amount\na,USD,1.00001",
    "id,currency,amount\na,USD,1000000001",
    "id,currency,amount\n\"a,USD,1",
    "id,currency,amount\n\"a\"x,USD,1",
    "wrong,header\na,USD,1"
}

)
{
    try
    {
        Csv.Parse(bad);
        throw new Exception("Invalid CSV accepted: " + bad);
    }
    catch (ArgumentException)
    {
    }
}

Check(decimal.Round(1.005m, 2, MidpointRounding.ToEven) == 1.00m, "banker rounding");
Console.WriteLine("10 CSV and precision checks passed.");
