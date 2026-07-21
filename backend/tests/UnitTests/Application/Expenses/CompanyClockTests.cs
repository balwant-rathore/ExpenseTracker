using Application.Expenses;
using Microsoft.Extensions.Options;

namespace UnitTests.Application.Expenses;

public class CompanyClockTests
{
    [Fact]
    public void Today_ReflectsConfiguredTimeZone_NotUtc()
    {
        var options = Options.Create(new CompanyTimeZoneOptions { TimeZoneId = "Asia/Kolkata" });
        var clock = new CompanyClock(options);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        var expected = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));

        Assert.Equal(expected, clock.Today());
    }

    [Fact]
    public void Today_UsesDefaultTimeZoneId_WhenNotConfigured()
    {
        var options = Options.Create(new CompanyTimeZoneOptions());

        Assert.Equal("Asia/Kolkata", options.Value.TimeZoneId);
    }
}
