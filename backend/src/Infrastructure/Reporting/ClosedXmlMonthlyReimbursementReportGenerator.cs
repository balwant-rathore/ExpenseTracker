using ClosedXML.Excel;
using Domain.Reporting;

namespace Infrastructure.Reporting;

public class ClosedXmlMonthlyReimbursementReportGenerator : IMonthlyReimbursementReportGenerator
{
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly string[] Headers =
    [
        "Employee", "Expense Number", "Category", "Amount", "Currency",
        "Approval Date", "Reimbursement Date",
    ];

    public byte[] Generate(IReadOnlyList<MonthlyReimbursementRecord> records)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Monthly Reimbursement");

        for (var col = 0; col < Headers.Length; col++)
        {
            worksheet.Cell(1, col + 1).Value = Headers[col];
        }

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var row = i + 2;
            worksheet.Cell(row, 1).Value = record.EmployeeName;
            worksheet.Cell(row, 2).Value = record.ExpenseNumber;
            worksheet.Cell(row, 3).Value = record.Category;
            worksheet.Cell(row, 4).Value = record.Amount;
            worksheet.Cell(row, 5).Value = record.Currency;
            SetDateCell(worksheet.Cell(row, 6), record.ApprovalDate);
            SetDateCell(worksheet.Cell(row, 7), record.ReimbursementDate);
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetDateCell(IXLCell cell, DateTime? value)
    {
        if (value is null)
        {
            return;
        }

        cell.Value = value.Value;
        cell.Style.DateFormat.Format = DateFormat;
    }
}
