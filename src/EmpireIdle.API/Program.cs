using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("game-config.json", optional: false, reloadOnChange: true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EmpireIdle API",
        Version = "v1",
        Description = "Browser-based idle empire builder game API"
    });
});

builder.Services.Configure<GameConfig>(builder.Configuration.GetSection("GameConfig"));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionString 'DefaultConnection' not found. Check User Secrets."); 

builder.Services.AddInfrastructure(builder.Configuration);

// Hangfire — використовує ту саму PostgreSQL базу
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(connectionString))); 
builder.Services.AddHangfireServer();
builder.Services.AddControllers();

builder.Services.AddExceptionHandler<EmpireIdle.API.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "EmpireIdle API v1");
    });
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

