using System.Text;
using SpendSense.Domain.Enums;
using SpendSense.Infrastructure.StatementParsers;

namespace SpendSense.UnitTests;

public sealed class StatementParserTests
{
    [Fact]
    public async Task CsvParser_ParsesIciciTransactionHistoryExport()
    {
        const string csv = """
Sl No.,Value Date,Transaction Date,Cheque Number,Transaction Remarks,Withdrawal Amount (INR ),Deposit Amount (INR ),Balance (INR )
1,02/07/2026,02/07/2026,,UPI/PAY/example merchant/ICICI,250.50,,1000.00
2,03/07/2026,03/07/2026,,NEFT CREDIT FROM CUSTOMER,,1000.00,2000.00
""";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var parser = new CsvStatementParser();

        var result = await parser.ParseAsync(stream);

        Assert.Equal(2, result.Count);
        Assert.Equal(DebitCredit.Debit, result[0].DebitCredit);
        Assert.Equal(250.50m, result[0].Amount);
        Assert.Equal(DebitCredit.Credit, result[1].DebitCredit);
        Assert.Equal(1000.00m, result[1].Amount);
    }
}
