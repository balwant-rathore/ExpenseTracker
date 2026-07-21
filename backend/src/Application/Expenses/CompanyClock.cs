using Microsoft.Extensions.Options;

namespace Application.Expenses;

public class CompanyClock : ICompanyClock
{
    private readonly TimeZoneInfo _timeZone;

    public CompanyClock(IOptions<CompanyTimeZoneOptions> options)
    {
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateOnly Today()
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
        return DateOnly.FromDateTime(localNow);
    }
}
