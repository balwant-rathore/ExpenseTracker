using Api.Authentication;
using Api.ErrorHandling;
using Api.Extensions;
using Domain.Repositories;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordResetOtpRepository, PasswordResetOtpRepository>();
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IAttachmentRepository, AttachmentRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

var employeeCsvPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["SeedData:EmployeeCsvPath"] ?? string.Empty));
builder.Services.AddSingleton(new EmployeeSeedOptions { EmployeeCsvPath = employeeCsvPath });
builder.Services.AddScoped<IEmployeeCsvParser, EmployeeCsvParser>();
builder.Services.AddScoped<EmployeeSeedPlanner>();
builder.Services.AddScoped<ISeedRunner, EmployeeCsvSeedRunner>();

builder.Services.AddAuthFoundation(builder.Configuration);
builder.Services.AddAttachmentFoundation(builder.Configuration);
builder.Services.AddNotificationFoundation(builder.Configuration);
builder.Services.AddExpenseFoundation(builder.Configuration);
builder.Services.AddReportFoundation();
builder.Services.AddDashboardFoundation();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    using var seedScope = app.Services.CreateScope();
    var seedRunner = seedScope.ServiceProvider.GetRequiredService<ISeedRunner>();
    await seedRunner.SeedAsync(CancellationToken.None);
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<EmployeeRoleResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
