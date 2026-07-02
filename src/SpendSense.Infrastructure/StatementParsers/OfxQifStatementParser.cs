using SpendSense.Application.Abstractions;

namespace SpendSense.Infrastructure.StatementParsers;

public sealed class OfxQifStatementParser : IStatementParser
{
    public string Name => "OfxQifStatementParser";

    public bool CanParse(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".ofx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".qfx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".qif", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("ofx", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("qif", StringComparison.OrdinalIgnoreCase);
    }

    public string DetectBank(string fileName, Stream content)
    {
        var text = StatementParsingSupport.ReadTextAsync(content, 4096).GetAwaiter().GetResult();
        return StatementParsingSupport.DetectBank(fileName, text);
    }

    public async Task<IReadOnlyList<ParsedTransaction>> ParseAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var text = await StatementParsingSupport.ReadTextAsync(content, cancellationToken: cancellationToken);
        return text.Contains("<STMTTRN>", StringComparison.OrdinalIgnoreCase)
            ? StatementParsingSupport.ParseOfx(text)
            : StatementParsingSupport.ParseQif(text);
    }
}
