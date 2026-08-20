using EmpireIdle.API.Hubs;
using EmpireIdle.API.Jobs;
using EmpireIdle.API.Middleware;
using EmpireIdle.API.Services;
using EmpireIdle.API.Swagger;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Granters;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure;
using EmpireIdle.Infrastructure.Auth;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

//  1. КОНФІГУРАЦІЯ

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
    .AddJsonFile("Config/items.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/quests.json", optional: false, reloadOnChange: true);

// ValidateOnStart перетворює 
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
    .Validate(c => c.ActiveServerIds.Count > 0, "GameConfig.ActiveServerIds is empty.")
    .Validate(c => c.ActiveServerIds.Contains(c.DefaultServerId), "GameConfig.DefaultServerId is not in ActiveServerIds.")
    .Validate(c => c.Quests.Count > 0, "GameConfig.Quests is empty — check Config/quests.json.")
    .Validate(c => c.Quests.All(q => q.Objectives.Count > 0), "GameConfig has a quest without objectives.")
    .Validate(c => c.Quests.Select(q => q.Key).Distinct().Count() == c.Quests.Count, "GameConfig.Quests has duplicate keys.")
    .Validate(c => c.Quests.All(q => q.Prerequisite is null || c.Quests.Any(p => p.Key == q.Prerequisite)), "A quest references a prerequisite that does not exist.")
    .Validate(c => c.Quests.SelectMany(q => q.Rewards).All(r => r.Type != "Resource" || r.Key is not null), "A resource reward is missing its Key.")
    .Validate(c => c.Quests.SelectMany(q => q.Rewards)
        .Where(r => r.Type == "Resource").All(r => c.Resources.Any(res => res.Key == r.Key)), "A quest reward references an unknown resource.")
    .Validate(c => c.Quests.SelectMany(q => q.Rewards.Select(r => (q.Key, r)))
        .Where(x => x.r.Type == "Item")
        .All(x => c.Items.Any(i => i.Key == x.r.Key)), "A quest reward references an unknown item — check Item keys in Config/quests.json against Config/items.json.")
    .Validate(c => c.Monetization.SpeedUpFactor > 0, "GameConfig.Monetization.SpeedUpFactor must be positive.")
    .Validate(c => c.Monetization.SpeedUpExponent is > 0 and < 1,
        "GameConfig.Monetization.SpeedUpExponent must be between 0 and 1 — otherwise long timers become unaffordable.")
    .ValidateOnStart();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(nameof(JwtSettings)));

// Знімки конфігу для сінглтонів — читаються тут, використовуються нижче
var gameConfig = builder.Configuration.GetSection("GameConfig").Get<GameConfig>()
    ?? throw new InvalidOperationException("GameConfig section is missing or invalid.");

var jwtSettings = builder.Configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings not configured.");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' not found. Check User Secrets.");

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("Cors:AllowedOrigins is not configured.");

//  2. ДОМЕННІ СЕРВІСИ
//  Усі через фабрику (sp => new ...): без цього об'єкт створюється
//  в момент реєстрації й падає раніше за ValidateOnStart.

builder.Services.AddSingleton(sp => new GameCatalog(gameConfig));
builder.Services.AddSingleton(sp => new TerrainGenerator(gameConfig.Map));
builder.Services.AddSingleton(sp => new CasualtySplitter(gameConfig.Combat));
builder.Services.AddSingleton(sp => new SpeedUpCalculator(gameConfig.Monetization));
builder.Services.AddSingleton(sp => new CombatCalculator(gameConfig.Combat, sp.GetRequiredService<GameCatalog>()));
builder.Services.AddSingleton(sp => new MonsterArmyBuilder(sp.GetRequiredService<GameCatalog>()));
builder.Services.AddSingleton(sp => new MonsterSpawner(sp.GetRequiredService<TerrainGenerator>(), gameConfig.Map, sp.GetRequiredService<GameCatalog>()));
builder.Services.AddSingleton(sp => new MarchCalculator(sp.GetRequiredService<TerrainGenerator>(), sp.GetRequiredService<GameCatalog>()));
builder.Services.AddSingleton(sp => new SettlementPlacer(sp.GetRequiredService<TerrainGenerator>(), gameConfig.Map));

//  3. ІНФРАСТРУКТУРА
//  БД, репозиторії, Identity, MediatR, Outbox — усе в одному місці.

builder.Services.AddInfrastructure(builder.Configuration);

//  4. КОНТЕКСТ ЗАПИТУ
//  Хто робить запит і в якому світі. Читається з JWT-клеймів.

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentPlayer, CurrentPlayer>();
builder.Services.AddScoped<IRequestContext, RequestContext>();
builder.Services.AddScoped<IServerContext, ServerContext>();

//  5. АУТЕНТИФІКАЦІЯ ТА АВТОРИЗАЦІЯ

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

        // SignalR не вміє слати заголовки при handshake — токен приходить у query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Усе, що явно не позначене [AllowAnonymous], вимагає автентифікації.
    // Забути [Authorize] на новому контролері тепер безпечно.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

//  6. ЗАХИСТ ПЕРИМЕТРА

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

const string FrontendCors = "FrontendCors";

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

//  7. ФОНОВІ ЗАДАЧІ
//  Hangfire живе в тій самій PostgreSQL базі.

builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

// Раннер створює scope на кожен активний світ — без нього
// query-фільтри не мають що застосувати у фоновому контексті
builder.Services.AddScoped<ServerJobRunner>();
builder.Services.AddScoped<TimerScanJob>();
builder.Services.AddScoped<MonsterSpawnJob>();
builder.Services.AddScoped<OutboxMaintenanceJob>();
builder.Services.AddScoped<DailyQuestResetJob>();

//  8. ВЕБ-ШАР

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddScoped<IGameNotifier, SignalRGameNotifier>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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

    options.OperationFilter<IdempotencyHeaderFilter>();
});

var app = builder.Build();

//  9. КОНВЕЄР ЗАПИТУ
//  Порядок критичний: кожен наступний крок покладається на попередній.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "EmpireIdle API v1"));

    // AllowAnonymous обходить FallbackPolicy — доступ вирішує сам фільтр
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter()]
    }).AllowAnonymous();
}
else
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseCors(FrontendCors);

// Перед автентифікацією: відсіюємо ще до роботи з токеном
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

//  10. РОЗКЛАД ФОНОВИХ ЗАДАЧ

RecurringJob.AddOrUpdate<TimerScanJob>("timer-scan", job => job.RunAsync(), Cron.Minutely);
RecurringJob.AddOrUpdate<MonsterSpawnJob>("monster-spawn", job => job.RunAsync(), "*/5 * * * *");
RecurringJob.AddOrUpdate<OutboxMaintenanceJob>("outbox-maintenance", job => job.RunAsync(), Cron.Hourly);
RecurringJob.AddOrUpdate<DailyQuestResetJob>("daily-quest-reset", job => job.RunAsync(), Cron.Daily);

app.Run();

/// <summary>Точка входу — public для WebApplicationFactory у тестах.</summary>
public partial class Program { }
