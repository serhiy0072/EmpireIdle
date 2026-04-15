using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("game-config.json", optional: false, reloadOnChange: true);

builder.Services.AddOpenApi();
builder.Services.Configure<GameConfig>(builder.Configuration.GetSection("GameConfig"));
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();

