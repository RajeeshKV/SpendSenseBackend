using ClosedXML.Excel;
using SpendSense.Application.Abstractions;

namespace SpendSense.Infrastructure.StatementParsers;

public sealed class ExcelStatementParser : IStatementParser
{
    public string Name => "ExcelStatementParser";

    public bool CanParse(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("excel", StringComparison.OrdinalIgnoreCase);
    }

    public string DetectBank(string fileName, Stream content)
    {
        var text = ReadWorkbookPreview(content, 100);
        return StatementParsingSupport.DetectBank(fileName, text);
    }

    public Task<IReadOnlyList<ParsedTransaction>> ParseAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (content.CanSeek) content.Position = 0;
        using var workbook = new XLWorkbook(content);
        foreach (var worksheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = worksheet.RowsUsed()
                .Take(5000)
                .Select(row => row.Cells(1, Math.Max(row.LastCellUsed()?.Address.ColumnNumber ?? 1, 1)).Select(cell => cell.GetFormattedString()).ToArray())
                .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                .ToArray();
            var parsed = StatementParsingSupport.ParseDelimitedRows(rows);
            if (parsed.Count > 0) return Task.FromResult(parsed);
        }

        return Task.FromResult<IReadOnlyList<ParsedTransaction>>(Array.Empty<ParsedTransaction>());
    }

    private static string ReadWorkbookPreview(Stream content, int maxRows)
    {
        if (content.CanSeek) content.Position = 0;
        using var workbook = new XLWorkbook(content);
        var values = workbook.Worksheets
            .SelectMany(sheet => sheet.RowsUsed().Take(maxRows))
            .SelectMany(row => row.CellsUsed().Select(cell => cell.GetFormattedString()))
            .Take(1000);
        if (content.CanSeek) content.Position = 0;
        return string.Join(' ', values);
    }
}
