using Infrastructure.Persistence;
using Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ISeedRunner, NoOpSeedRunner>();

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
