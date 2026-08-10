using EmpireIdle.API.Hubs;
using EmpireIdle.API.Jobs;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure;
using EmpireIdle.Infrastructure.Auth;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EmpireIdle API",
        Version = "v1",
        Description = "Browser-based idle empire builder game API"
    });


    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введи JWT токен (без слова Bearer)"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
    options.OperationFilter<EmpireIdle.API.Swagger.IdempotencyHeaderFilter>();
});

builder.Services.AddOptions<GameConfig>()
    .Bind(builder.Configuration.GetSection("GameConfig"))
    .Validate(c => c.Resources.Count > 0, "GameConfig.Resources is empty — check Config/resources.json.")
    .Validate(c => c.Buildings.Count > 0, "GameConfig.Buildings is empty — check Config/buildings.json.")
    .Validate(c => c.Units.Count > 0, "GameConfig.Units is empty — check Config/units.json.")
    .Validate(c => c.Zones.Count > 0, "GameConfig.Zones is empty — check Config/zones.json.")
    .Validate(c => c.Monsters.Count > 0, "GameConfig.Monsters is empty — check Config/monsters.json.")
    .Validate(c => c.Items.Count > 0, "GameConfig.Items is empty — check Config/items.json.")
    .Validate(c => c.Shop.GemPacks.Count > 0, "GameConfig.Shop.GemPacks is empty — check Config/shop.json.")
    .Validate(c => c.Map.Terrains.Any(t => t.Weight > 0), "GameConfig.Map has no terrain with positive weight.")
    .ValidateOnStart();

builder.Services.Configure<GameConfig>(builder.Configuration.GetSection("GameConfig"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(nameof(JwtSettings)));

var jwtSettings = builder.Configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>()
        ?? throw new InvalidOperationException("JWT settings not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };

        // SignalR передає JWT через query string (?access_token=...)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

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

builder.Services.AddSignalR();
builder.Services.AddScoped<IGameNotifier, SignalRGameNotifier>();
builder.Services.AddScoped<ResourceTickJob>();
builder.Services.AddScoped<TimerScanJob>();

var gameConfig = builder.Configuration.GetSection("GameConfig").Get<GameConfig>()
    ?? throw new InvalidOperationException("GameConfig section is missing or invalid.");

builder.Services.AddSingleton(new TerrainGenerator(gameConfig.Map));
builder.Services.AddSingleton(sp => new MonsterSpawner(sp.GetRequiredService<TerrainGenerator>(), gameConfig.Map, gameConfig.Monsters));
builder.Services.AddSingleton(sp => new MarchCalculator(sp.GetRequiredService<TerrainGenerator>(), gameConfig.Units));
builder.Services.AddSingleton(new CombatCalculator(gameConfig.Combat, gameConfig.Units));
builder.Services.AddSingleton(new MonsterArmyBuilder(gameConfig.Monsters));
builder.Services.AddSingleton(new CasualtySplitter(gameConfig.Combat));
builder.Services.AddSingleton(sp => new SettlementPlacer(sp.GetRequiredService<TerrainGenerator>(), gameConfig.Map));
builder.Services.AddSingleton(new SpeedUpCalculator(gameConfig.Monetization));

builder.Services.AddScoped<MonsterSpawnJob>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentPlayer, EmpireIdle.API.Services.CurrentPlayer>();
builder.Services.AddScoped<IRequestContext, EmpireIdle.API.Services.RequestContext>();

const string FrontendCors = "FrontendCors";
builder.Services.AddCors(option =>
    option.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
.AllowCredentials()
));

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

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseExceptionHandler();

app.UseCors(FrontendCors);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHub<GameHub>("/hubs/game");

// Recurring job — тік ресурсів кожну хвилину
RecurringJob.AddOrUpdate<ResourceTickJob>("resource-tick", job => job.RunAsync(), Cron.Minutely);
RecurringJob.AddOrUpdate<TimerScanJob>("timer-scan", job => job.RunAsync(), Cron.Minutely);
RecurringJob.AddOrUpdate<MonsterSpawnJob>("monster-spawn", job => job.RunAsync(), "*/5 * * * *");

app.Run();

