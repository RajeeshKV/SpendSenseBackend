using System.Text;
using SpendSense.Application.Abstractions;
using UglyToad.PdfPig;

namespace SpendSense.Infrastructure.StatementParsers;

public sealed class PdfTextStatementParser : IStatementParser
{
    public string Name => "PdfTextStatementParser";

    public bool CanParse(string fileName, string contentType) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

    public string DetectBank(string fileName, Stream content)
    {
        var text = ExtractText(content, 3);
        return StatementParsingSupport.DetectBank(fileName, text);
    }

    public Task<IReadOnlyList<ParsedTransaction>> ParseAsync(Stream content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = ExtractText(content, maxPages: 0);
        var parsed = StatementParsingSupport.ParseLooseText(text);
        return Task.FromResult(parsed);
    }

    internal static string ExtractText(Stream content, int maxPages)
    {
        if (content.CanSeek) content.Position = 0;
        using var document = PdfDocument.Open(content);
        var builder = new StringBuilder();
        var pages = maxPages <= 0 ? document.GetPages() : document.GetPages().Take(maxPages);
        foreach (var page in pages)
        {
            builder.AppendLine(page.Text);
        }

        if (content.CanSeek) content.Position = 0;
        return builder.ToString();
    }
}
