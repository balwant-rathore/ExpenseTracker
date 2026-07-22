using ClosedXML.Excel;
using Domain.Reporting;
using Infrastructure.Reporting;

namespace UnitTests.Infrastructure.Reporting;

public class ClosedXmlMonthlyReimbursementReportGeneratorTests
{
    private readonly ClosedXmlMonthlyReimbursementReportGenerator _generator = new();

    [Fact]
    public void Generate_EmptyRecords_ReturnsHeaderOnlyWorkbook()
    {
        var bytes = _generator.Generate([]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();

        Assert.Equal("Employee", worksheet.Cell(1, 1).GetString());
        Assert.True(worksheet.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public void Generate_HeaderRow_MatchesFrsFieldOrder()
    {
        var bytes = _generator.Generate([]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();

        Assert.Equal("Employee", worksheet.Cell(1, 1).GetString());
        Assert.Equal("Expense Number", worksheet.Cell(1, 2).GetString());
        Assert.Equal("Category", worksheet.Cell(1, 3).GetString());
        Assert.Equal("Amount", worksheet.Cell(1, 4).GetString());
        Assert.Equal("Currency", worksheet.Cell(1, 5).GetString());
        Assert.Equal("Approval Date", worksheet.Cell(1, 6).GetString());
        Assert.Equal("Reimbursement Date", worksheet.Cell(1, 7).GetString());
    }

    [Fact]
    public void Generate_SingleRecord_WritesRowValuesAndDateFormatting()
    {
        var approvalDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var reimbursementDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var record = new MonthlyReimbursementRecord(
            "Test Employee",
            "EXP-20260710-0001",
            "Travel",
            100.50m,
            "INR",
            approvalDate,
            reimbursementDate);

        var bytes = _generator.Generate([record]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();

        Assert.Equal("Test Employee", worksheet.Cell(2, 1).GetString());
        Assert.Equal("EXP-20260710-0001", worksheet.Cell(2, 2).GetString());
        Assert.Equal("Travel", worksheet.Cell(2, 3).GetString());
        Assert.Equal(100.50, worksheet.Cell(2, 4).GetDouble());
        Assert.Equal("INR", worksheet.Cell(2, 5).GetString());
        Assert.Equal(approvalDate, worksheet.Cell(2, 6).GetDateTime());
        Assert.Equal("yyyy-MM-dd", worksheet.Cell(2, 6).Style.DateFormat.Format);
        Assert.Equal(reimbursementDate, worksheet.Cell(2, 7).GetDateTime());
        Assert.Equal("yyyy-MM-dd", worksheet.Cell(2, 7).Style.DateFormat.Format);
    }
}
