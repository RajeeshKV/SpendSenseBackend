using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpendSense.Application.Abstractions;
using SpendSense.Domain.Enums;

namespace SpendSense.Infrastructure.StatementParsers;

internal static partial class StatementParsingSupport
{
    private static readonly BankSignature[] BankSignatures =
    [
        new("HDFC Bank", ["hdfc", "hdfcbank"]),
        new("ICICI Bank", ["icici", "icicibank"]),
        new("State Bank of India", ["sbi", "statebankofindia", "state bank of india"]),
        new("Axis Bank", ["axis", "axisbank"]),
        new("Kotak Mahindra Bank", ["kotak", "kotakmahindra"]),
        new("Federal Bank", ["federal", "federalbank"]),
        new("Canara Bank", ["canara", "canarabank"]),
        new("Punjab National Bank", ["pnb", "punjabnationalbank", "punjab national bank"]),
        new("Bank of Baroda", ["bob", "bankofbaroda", "bank of baroda"]),
        new("Union Bank of India", ["unionbank", "union bank"]),
        new("Bank of India", ["bankofindia", "bank of india"]),
        new("IDFC First Bank", ["idfc", "idfcfirst"]),
        new("IndusInd Bank", ["indusind", "indusindbank"]),
        new("Yes Bank", ["yesbank", "yes bank"]),
        new("RBL Bank", ["rbl", "rblbank"]),
        new("AU Small Finance Bank", ["aubank", "au small finance", "aufincare"]),
        new("Standard Chartered Bank", ["standardchartered", "standard chartered", "scb"]),
        new("HSBC Bank", ["hsbc"]),
        new("Citibank", ["citi", "citibank"]),
        new("Indian Bank", ["indianbank", "indian bank"]),
        new("Central Bank of India", ["centralbank", "central bank of india"])
    ];

    internal static readonly string[] DateColumns =
    [
        "date", "transactiondate", "transaction date", "txndate", "txn date", "trandate", "tran date",
        "valuedate", "value date", "postingdate", "posting date"
    ];

    internal static readonly string[] DescriptionColumns =
    [
        "description", "narration", "particulars", "details", "transactionremarks", "transaction remarks",
        "transactiondetails", "transaction details", "remarks", "payee", "merchant", "memo", "name"
    ];

    internal static readonly string[] MerchantColumns = ["merchant", "payee", "name", "description", "narration", "particulars", "details"];
    internal static readonly string[] AmountColumns = ["amount", "transactionamount", "transaction amount", "amt"];
    internal static readonly string[] DebitColumns = ["debit", "withdrawal", "withdrawalamt", "withdrawal amt", "withdrawal amount", "debitamount", "debit amount", "dr", "paidout"];
    internal static readonly string[] CreditColumns = ["credit", "deposit", "depositamt", "deposit amt", "deposit amount", "creditamount", "credit amount", "cr", "paidin"];
    internal static readonly string[] TypeColumns = ["type", "drcr", "dr/cr", "debitcredit", "debit/credit", "crdr", "cr/dr"];

    private static readonly HashSet<string> NormalizedDateColumns = DateColumns.Select(NormalizeHeader).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> NormalizedDescriptionColumns = DescriptionColumns.Select(NormalizeHeader).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> NormalizedAmountColumns = AmountColumns.Concat(DebitColumns).Concat(CreditColumns).Select(NormalizeHeader).ToHashSet(StringComparer.Ordinal);

    internal static string DetectBank(string fileName, string text)
    {
        var searchable = NormalizeForSearch(fileName + " " + text);
        var signature = BankSignatures.FirstOrDefault(x => x.Aliases.Any(searchable.Contains));
        return signature?.Name ?? "Generic Bank Statement";
    }

    internal static async Task<string> ReadTextAsync(Stream content, int maxChars = 60000, CancellationToken cancellationToken = default)
    {
        if (content.CanSeek) content.Position = 0;
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var buffer = new char[maxChars];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        if (content.CanSeek) content.Position = 0;
        return new string(buffer, 0, read);
    }

    internal static IReadOnlyList<ParsedTransaction> ParseDelimitedRows(IReadOnlyList<string[]> rows)
    {
        if (rows.Count == 0) return Array.Empty<ParsedTransaction>();
        var headerIndex = FindHeaderIndex(rows);
        if (headerIndex < 0) return Array.Empty<ParsedTransaction>();

        var columns = rows[headerIndex]
            .Select((name, index) => new { Name = NormalizeHeader(name), Index = index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name)
            .ToDictionary(x => x.Key, x => x.First().Index);

        var results = new List<ParsedTransaction>();
        foreach (var row in rows.Skip(headerIndex + 1))
        {
            var date = ReadDate(row, columns, DateColumns);
            var description = Read(row, columns, DescriptionColumns);
            var merchant = Read(row, columns, MerchantColumns);
            var amountText = Read(row, columns, AmountColumns);
            var debitText = Read(row, columns, DebitColumns);
            var creditText = Read(row, columns, CreditColumns);
            var typeText = Read(row, columns, TypeColumns);
            var (amount, type) = ResolveAmount(amountText, debitText, creditText, typeText);

            if (date.HasValue && amount > 0 && !string.IsNullOrWhiteSpace(description))
            {
                results.Add(new ParsedTransaction(date.Value, description, string.IsNullOrWhiteSpace(merchant) ? description : merchant, amount, type));
            }
        }

        return results;
    }

    internal static IReadOnlyList<ParsedTransaction> ParseLooseText(string text)
    {
        var results = new List<ParsedTransaction>();
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var match = LooseTransactionLineRegex().Match(line);
            if (!match.Success) continue;
            if (!TryParseDate(match.Groups["date"].Value, out var date)) continue;
            if (!TryParseMoney(match.Groups["amount"].Value, out var amount)) continue;

            var description = match.Groups["description"].Value.Trim(" -|\t".ToCharArray());
            if (string.IsNullOrWhiteSpace(description)) continue;
            var typeToken = NormalizeForSearch(line);
            var type = amount < 0 || typeToken.Contains(" debit") || typeToken.Contains(" dr") ? DebitCredit.Debit : DebitCredit.Credit;
            results.Add(new ParsedTransaction(date, description, description, Math.Abs(amount), type));
        }

        return results;
    }

    internal static IReadOnlyList<ParsedTransaction> ParseQif(string text)
    {
        var results = new List<ParsedTransaction>();
        foreach (var entry in text.Split('^', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            DateOnly? date = null;
            decimal? amount = null;
            var description = string.Empty;
            foreach (var line in entry.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length < 2) continue;
                var value = line[1..].Trim();
                if (line[0] == 'D' && TryParseDate(value, out var parsedDate)) date = parsedDate;
                if (line[0] == 'T' && TryParseMoney(value, out var parsedAmount)) amount = parsedAmount;
                if (line[0] is 'P' or 'M' && string.IsNullOrWhiteSpace(description)) description = value;
            }

            if (date.HasValue && amount.HasValue && !string.IsNullOrWhiteSpace(description))
            {
                results.Add(new ParsedTransaction(date.Value, description, description, Math.Abs(amount.Value), amount.Value < 0 ? DebitCredit.Debit : DebitCredit.Credit));
            }
        }

        return results;
    }

    internal static IReadOnlyList<ParsedTransaction> ParseOfx(string text)
    {
        var results = new List<ParsedTransaction>();
        foreach (Match block in OfxTransactionRegex().Matches(text))
        {
            var dateText = ReadOfxTag(block.Value, "DTPOSTED");
            var amountText = ReadOfxTag(block.Value, "TRNAMT");
            var name = ReadOfxTag(block.Value, "NAME");
            var memo = ReadOfxTag(block.Value, "MEMO");
            var description = string.IsNullOrWhiteSpace(name) ? memo : name;
            if (dateText.Length >= 8 && DateOnly.TryParseExact(dateText[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && TryParseMoney(amountText, out var amount) && !string.IsNullOrWhiteSpace(description))
            {
                results.Add(new ParsedTransaction(date, description, description, Math.Abs(amount), amount < 0 ? DebitCredit.Debit : DebitCredit.Credit));
            }
        }

        return results;
    }

    internal static IReadOnlyList<ParsedTransaction> ParseAiJson(string json)
    {
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start < 0 || end <= start) return Array.Empty<ParsedTransaction>();
        using var document = JsonDocument.Parse(json[start..(end + 1)]);
        if (!document.RootElement.TryGetProperty("transactions", out var transactions) || transactions.ValueKind != JsonValueKind.Array) return Array.Empty<ParsedTransaction>();

        var results = new List<ParsedTransaction>();
        foreach (var item in transactions.EnumerateArray())
        {
            var dateText = ReadJsonString(item, "date");
            var description = ReadJsonString(item, "description");
            var merchant = ReadJsonString(item, "merchant");
            var typeText = ReadJsonString(item, "debitCredit");
            if (!DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            if (!TryReadJsonDecimal(item, "amount", out var amount) || amount <= 0) continue;
            var type = typeText.Equals("Credit", StringComparison.OrdinalIgnoreCase) ? DebitCredit.Credit : DebitCredit.Debit;
            results.Add(new ParsedTransaction(date, description, string.IsNullOrWhiteSpace(merchant) ? description : merchant, amount, type));
        }

        return results;
    }

    internal static string[] SplitDelimitedLine(string line, char delimiter)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                values.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(ch);
            }
        }

        values.Add(builder.ToString());
        return values.ToArray();
    }

    internal static string NormalizeForSearch(string value) => value.ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty);

    private static int FindHeaderIndex(IReadOnlyList<string[]> rows)
    {
        for (var i = 0; i < Math.Min(rows.Count, 40); i++)
        {
            var normalized = rows[i].Select(NormalizeHeader).ToArray();
            var hasDate = normalized.Any(NormalizedDateColumns.Contains);
            var hasDescription = normalized.Any(NormalizedDescriptionColumns.Contains);
            var hasAmount = normalized.Any(NormalizedAmountColumns.Contains);
            if (hasDate && hasDescription && hasAmount) return i;
        }

        return -1;
    }

    private static string Read(string[] parts, Dictionary<string, int> columns, params string[] names)
    {
        foreach (var name in names.Select(NormalizeHeader))
        {
            if (columns.TryGetValue(name, out var index) && index < parts.Length) return parts[index].Trim().Trim('"');
        }
        return string.Empty;
    }

    private static DateOnly? ReadDate(string[] parts, Dictionary<string, int> columns, params string[] names)
    {
        var value = Read(parts, columns, names);
        return TryParseDate(value, out var date) ? date : null;
    }

    private static bool TryParseDate(string value, out DateOnly date)
    {
        string[] formats =
        [
            "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "dd-MMM-yyyy", "dd MMM yyyy",
            "dd/MM/yy", "dd-MM-yy", "MM/dd/yy", "yyyy/MM/dd", "dd.MM.yyyy", "dd.MM.yy"
        ];
        return DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
               DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static (decimal Amount, DebitCredit Type) ResolveAmount(string amountText, string debitText, string creditText, string typeText)
    {
        if (TryParseMoney(debitText, out var debit) && debit > 0) return (debit, DebitCredit.Debit);
        if (TryParseMoney(creditText, out var credit) && credit > 0) return (credit, DebitCredit.Credit);
        if (!TryParseMoney(amountText, out var amount)) return (0, DebitCredit.Debit);

        var normalizedType = NormalizeForSearch(typeText);
        if (normalizedType.Contains("debit") || normalizedType == "dr" || normalizedType.Contains("withdrawal")) return (Math.Abs(amount), DebitCredit.Debit);
        if (normalizedType.Contains("credit") || normalizedType == "cr" || normalizedType.Contains("deposit")) return (Math.Abs(amount), DebitCredit.Credit);
        return (Math.Abs(amount), amount < 0 ? DebitCredit.Debit : DebitCredit.Credit);
    }

    private static bool TryParseMoney(string value, out decimal amount)
    {
        var cleaned = value.Replace(",", string.Empty).Replace("INR", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("Rs.", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("?", string.Empty).Trim();
        var isDebit = cleaned.EndsWith("dr", StringComparison.OrdinalIgnoreCase) || cleaned.StartsWith("-", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("Dr", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("Cr", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var parsed = decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        if (parsed && isDebit) amount = -Math.Abs(amount);
        return parsed;
    }

    private static string ReadOfxTag(string text, string tag)
    {
        var match = Regex.Match(text, $"<{tag}>([^<\\r\\n]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ReadJsonString(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool TryReadJsonDecimal(JsonElement item, string name, out decimal value)
    {
        value = 0;
        if (!item.TryGetProperty(name, out var element)) return false;
        if (element.ValueKind == JsonValueKind.Number) return element.TryGetDecimal(out value);
        return element.ValueKind == JsonValueKind.String && TryParseMoney(element.GetString() ?? string.Empty, out value);
    }

    private static string NormalizeHeader(string value) => new(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    [GeneratedRegex(@"(?<date>\d{1,2}[-/. ][A-Za-z]{3}[-/. ]\d{2,4}|\d{1,2}[-/.]\d{1,2}[-/.]\d{2,4}|\d{4}[-/.]\d{1,2}[-/.]\d{1,2})\s+(?<description>.*?)\s+(?<amount>-?(?:Rs\.?|INR)?\s?\d[\d,]*\.\d{0,2}|-?\d[\d,]*)\s*(?:Cr|Dr|Credit|Debit)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LooseTransactionLineRegex();

    [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex OfxTransactionRegex();

    private sealed record BankSignature(string Name, string[] Aliases);
}

