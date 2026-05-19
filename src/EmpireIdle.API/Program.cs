using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("game-config.json", optional: false, reloadOnChange: true);

builder.Services.AddOpenApi();
builder.Services.Configure<GameConfig>(builder.Configuration.GetSection("GameConfig"));
builder.Services.AddInfrastructure(builder.Configuration);

// Hangfire — використовує ту саму PostgreSQL базу
builder.Services.AddHangfire(config => 
    config.UsePostgreSqlStorage(options => 
            options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();
builder.Services.AddControllers();

builder.Services.AddExceptionHandler<EmpireIdle.API.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapHangfireDashboard("/hangfire");
}

app.UseHttpsRedirection();
app.UseExceptionHandler();

// Recurring job — тік ресурсів кожну хвилину
RecurringJob.AddOrUpdate<IResourceTickService>(
    "resource-tick",
    service => service.TickAllVillagesAsync(CancellationToken.None),
    Cron.Minutely);
app.MapControllers();

app.Run();

