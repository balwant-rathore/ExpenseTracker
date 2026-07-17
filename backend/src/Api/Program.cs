using Domain.Repositories;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var seedScope = app.Services.CreateScope();
    var seedRunner = seedScope.ServiceProvider.GetRequiredService<ISeedRunner>();
    await seedRunner.SeedAsync(CancellationToken.None);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
