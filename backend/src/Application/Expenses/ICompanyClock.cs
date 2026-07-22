namespace Application.Expenses;

public interface ICompanyClock
{
    DateOnly Today();

    DateTime ConvertLocalToUtc(DateTime localDateTime);
}
