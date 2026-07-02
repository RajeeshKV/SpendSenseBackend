using System.Globalization;
using SpendSense.Application.Abstractions;
using SpendSense.Domain.Enums;

namespace SpendSense.Infrastructure.StatementParsers;

public sealed class CsvStatementParser : IStatementParser
{
    public string Name => "GenericCsvParser";
    public bool CanParse(string fileName, string contentType) => Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) || contentType.Contains("csv", StringComparison.OrdinalIgnoreCase);
    public string DetectBank(string fileName, Stream content) => "Generic";

    public async Task<IReadOnlyList<ParsedTransaction>> ParseAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var header = await reader.ReadLineAsync(cancellationToken);
        if (header is null) return Array.Empty<ParsedTransaction>();
        var columns = header.Split(',').Select((name, index) => new { Name = name.Trim().ToLowerInvariant(), Index = index }).ToDictionary(x => x.Name, x => x.Index);
        var results = new List<ParsedTransaction>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            var date = ReadDate(parts, columns, "date", "transactiondate", "txn date");
            var description = Read(parts, columns, "description", "narration", "details");
            var merchant = Read(parts, columns, "merchant", "payee", "description", "narration");
            var amountText = Read(parts, columns, "amount");
            var debitText = Read(parts, columns, "debit", "withdrawal");
            var creditText = Read(parts, columns, "credit", "deposit");
            var (amount, type) = ResolveAmount(amountText, debitText, creditText);
            if (date.HasValue && amount > 0) results.Add(new ParsedTransaction(date.Value, description, merchant, amount, type));
        }
        return results;
    }

    private static string Read(string[] parts, Dictionary<string, int> columns, params string[] names)
    {
        foreach (var name in names)
        {
            if (columns.TryGetValue(name, out var index) && index < parts.Length) return parts[index].Trim().Trim('"');
        }
        return string.Empty;
    }

    private static DateOnly? ReadDate(string[] parts, Dictionary<string, int> columns, params string[] names)
    {
        var value = Read(parts, columns, names);
        string[] formats = ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM/dd/yyyy"];
        return DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || DateOnly.TryParse(value, out date) ? date : null;
    }

    private static (decimal Amount, DebitCredit Type) ResolveAmount(string amountText, string debitText, string creditText)
    {
        if (decimal.TryParse(debitText, NumberStyles.Any, CultureInfo.InvariantCulture, out var debit) && debit > 0) return (debit, DebitCredit.Debit);
        if (decimal.TryParse(creditText, NumberStyles.Any, CultureInfo.InvariantCulture, out var credit) && credit > 0) return (credit, DebitCredit.Credit);
        if (decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)) return (Math.Abs(amount), amount < 0 ? DebitCredit.Debit : DebitCredit.Credit);
        return (0, DebitCredit.Debit);
    }
}
