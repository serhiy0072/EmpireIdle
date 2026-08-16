using EmpireIdle.API.Hubs;
using EmpireIdle.API.Jobs;
using EmpireIdle.API.Middleware;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure;
using EmpireIdle.Infrastructure.Auth;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Threading.RateLimiting;

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

builder.Configuration
    .AddJsonFile("game-config.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/resources.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/buildings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/units.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/monsters.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/map.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/combat.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/monetization.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/shop.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/items.json", optional: false, reloadOnChange: true);

builder.Services.AddOptions<GameConfig>()
    .Bind(builder.Configuration.GetSection("GameConfig"))
    .Validate(c => c.Resources.Count > 0, "GameConfig.Resources is empty — check Config/resources.json.")
    .Validate(c => c.Buildings.Count > 0, "GameConfig.Buildings is empty — check Config/buildings.json.")
    .Validate(c => c.Units.Count > 0, "GameConfig.Units is empty — check Config/units.json.")
    .Validate(c => c.Monsters.Count > 0, "GameConfig.Monsters is empty — check Config/monsters.json.")
    .Validate(c => c.Items.Count > 0, "GameConfig.Items is empty — check Config/items.json.")
    .Validate(c => c.Shop.GemPacks.Count > 0, "GameConfig.Shop.GemPacks is empty — check Config/shop.json.")
    .Validate(c => c.Map.Terrains.Any(t => t.Weight > 0), "GameConfig.Map has no terrain with positive weight.")
    .Validate(c => c.Buildings.Count(b => b.IsMainBuilding) == 1, "GameConfig must define exactly one main building.")
    .Validate(c => c.StartingResources.Count > 0, "GameConfig.StartingResources is empty.")
    .Validate(c => c.StartingResources.Keys.All(k => c.Resources.Any(r => r.Key == k)), "GameConfig.StartingResources references an unknown resource key.")
    .Validate(c => c.StartingBuildings.Count > 0, "GameConfig.StartingBuildings is empty.")
    .Validate(c => c.StartingBuildings.All(k => c.Buildings.Any(b => b.Key == k)), "GameConfig.StartingBuildings references a building key that does not exist.")
    .Validate(c => c.ScanBatchSize > 0, "GameConfig.ScanBatchSize must be greater than zero.")
    .ValidateOnStart();

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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Логін і реєстрація — окремо й жорстко: це поверхня для брутфорсу
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    // Решта API — по гравцю, а за його відсутності по IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("playerId")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthorization(options =>
{
    // Усе, що явно не позначене [AllowAnonymous], вимагає автентифікації.
    // Забути [Authorize] на новому контролері тепер безпечно — за замовчуванням він закритий.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

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
builder.Services.AddScoped<OutboxMaintenanceJob>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentPlayer, EmpireIdle.API.Services.CurrentPlayer>();
builder.Services.AddScoped<IRequestContext, EmpireIdle.API.Services.RequestContext>();

const string FrontendCors = "FrontendCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("Cors:AllowedOrigins is not configured.");

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "EmpireIdle API v1");
    });
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter()]
    }).AllowAnonymous();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseExceptionHandler();

app.UseCors(FrontendCors);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<GameHub>("/hubs/game");

// Recurring job — тік ресурсів кожну хвилину
RecurringJob.AddOrUpdate<ResourceTickJob>("resource-tick", job => job.RunAsync(), Cron.Minutely);
RecurringJob.AddOrUpdate<TimerScanJob>("timer-scan", job => job.RunAsync(), Cron.Minutely);
RecurringJob.AddOrUpdate<MonsterSpawnJob>("monster-spawn", job => job.RunAsync(), "*/5 * * * *");
RecurringJob.AddOrUpdate<OutboxMaintenanceJob>("outbox-maintenance", job => job.RunAsync(), Cron.Hourly);

app.Run();



/// <summary>Точка входу — public для WebApplicationFactory у тестах.</summary>
public partial class Program { }

