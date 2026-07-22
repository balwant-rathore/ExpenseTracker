namespace Domain.Reporting;

public interface IMonthlyReimbursementReportGenerator
{
    byte[] Generate(IReadOnlyList<MonthlyReimbursementRecord> records);
}
