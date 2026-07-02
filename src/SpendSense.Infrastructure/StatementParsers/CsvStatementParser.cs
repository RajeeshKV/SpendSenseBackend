using SpendSense.Application.Abstractions;

namespace SpendSense.Infrastructure.StatementParsers;

public sealed class CsvStatementParser : IStatementParser
{
    public string Name => "BankAwareCsvParser";

    public bool CanParse(string fileName, string contentType) =>
        Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(fileName).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ||
        contentType.Contains("csv", StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
        contentType.Contains("tab-separated", StringComparison.OrdinalIgnoreCase);

    public string DetectBank(string fileName, Stream content)
    {
        var text = StatementParsingSupport.ReadTextAsync(content, 4096).GetAwaiter().GetResult();
        return StatementParsingSupport.DetectBank(fileName, text);
    }

    public async Task<IReadOnlyList<ParsedTransaction>> ParseAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var text = await StatementParsingSupport.ReadTextAsync(content, cancellationToken: cancellationToken);
        var delimiter = PathLooksTabSeparated(text) ? '\t' : ',';
        var rows = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => StatementParsingSupport.SplitDelimitedLine(x, delimiter))
            .ToArray();

        var parsed = StatementParsingSupport.ParseDelimitedRows(rows);
        return parsed.Count > 0 ? parsed : StatementParsingSupport.ParseLooseText(text);
    }

    private static bool PathLooksTabSeparated(string text)
    {
        var firstLine = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return firstLine.Count(x => x == '\t') > firstLine.Count(x => x == ',');
    }
}
