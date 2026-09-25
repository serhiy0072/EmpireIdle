using EmpireIdle.API.Hubs;
using EmpireIdle.API.Jobs;
using EmpireIdle.API.Middleware;
using EmpireIdle.API.Services;
using EmpireIdle.API.Swagger;
using EmpireIdle.Application.Catalog;
using EmpireIdle.Application.Dev.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure;
using EmpireIdle.Infrastructure.Auth;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
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
    .AddJsonFile("Config/quests.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/rating.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/clan.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/heroes.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/dungeons.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/market.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/chat.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/login-rewards.json", optional: false, reloadOnChange: true)
    .AddJsonFile("Config/locales.en.json", optional: false, reloadOnChange: true);

// Наповненість секцій і межі окремих полів. Узгодженість між секціями —
// у GameCatalog.Validate: правило пошуку однозначне, і два списки не розійдуться.
builder.Services.AddOptions<GameConfig>()
    .Bind(builder.Configuration.GetSection("GameConfig"))
    .Validate(c => c.Resources.Count > 0, "GameConfig.Resources is empty — check Config/resources.json.")
    .Validate(c => c.Buildings.Count > 0, "GameConfig.Buildings is empty — check Config/buildings.json.")
    .Validate(c => c.Units.Count > 0, "GameConfig.Units is empty — check Config/units.json.")
    .Validate(c => c.Monsters.Count > 0, "GameConfig.Monsters is empty — check Config/monsters.json.")
    .Validate(c => c.Items.Count > 0, "GameConfig.Items is empty — check Config/items.json.")
    .Validate(c => c.Quests.Count > 0, "GameConfig.Quests is empty — check Config/quests.json.")
    .Validate(c => c.Quests.All(q => q.Objectives.Count > 0), "GameConfig has a quest without objectives.")
    .Validate(c => c.Quests.SelectMany(q => q.Rewards.Concat(q.RewardTiers.SelectMany(t => t.Rewards))).All(r => r.Amount > 0), "GameConfig has a quest reward with non-positive Amount.")
    .Validate(c => c.Shop.GemPacks.Count > 0, "GameConfig.Shop.GemPacks is empty — check Config/shop.json.")
    .Validate(c => c.Shop.Items.All(i => i.PriceGems > 0 && i.MaxPerPurchase is >= 1 and <= 100), "GameConfig.Shop.Items has an offer with a non-positive price or MaxPerPurchase outside 1–100.")
    .Validate(c => c.StartingResources.Count > 0, "GameConfig.StartingResources is empty.")
    .Validate(c => c.ActiveServerIds.Count > 0, "GameConfig.ActiveServerIds is empty.")
    .Validate(c => c.Map.Terrains.Any(t => t.Weight > 0), "GameConfig.Map has no terrain with positive weight.")
    .Validate(c => c.Map.Width > 0 && c.Map.Height > 0, "GameConfig.Map has non-positive dimensions.")
    .Validate(c => c.Map.CellsPerMonster > 0, "GameConfig.Map.CellsPerMonster must be positive.")
    .Validate(c => c.ScanBatchSize > 0, "GameConfig.ScanBatchSize must be greater than zero.")
    .Validate(c => c.Monetization.SpeedUpFloorSeconds >= 0, "GameConfig.Monetization.SpeedUpFloorSeconds cannot be negative.")
    .Validate(c => c.Monetization.SpeedUpFactor > 0, "GameConfig.Monetization.SpeedUpFactor must be positive.")
    .Validate(c => c.Monetization.SpeedUpExponent is > 0 and < 1, "GameConfig.Monetization.SpeedUpExponent must be between 0 and 1 — otherwise long timers become unaffordable.")
    .Validate(c => c.Combat.PreviewOddsThresholds.Count > 0, "GameConfig.Combat.PreviewOddsThresholds is empty — every battle preview would return the worst band.")
    .Validate(c => c.Clan.Capacity > 0, "GameConfig.Clan.Capacity must be positive — nobody could join a clan.")
    .Validate(c => c.Heroes.Count > 0, "GameConfig.Heroes is empty — check Config/heroes.json.")
    .Validate(c => c.Equipment.ArtifactSets.All(s => !string.IsNullOrWhiteSpace(s.DisplayName)), "GameConfig.Equipment.ArtifactSets must all have a DisplayName — the sets screen shows it to players.")
    .Validate(c => c.Equipment.ArtifactFocusWeight >= 1.0, "GameConfig.Equipment.ArtifactFocusWeight must be at least 1 — below it the focus stats would be rarer than the rest.")
    .Validate(c => c.Equipment.ArtifactTierMultipliers.All(m => m > 0), "GameConfig.Equipment.ArtifactTierMultipliers must be positive — otherwise higher-tier artifacts roll zero stats.")
    .Validate(c => c.Mail.LetterRetentionDays > 0 && c.Mail.AnnouncementRetentionDays > 0 && c.Mail.RewardRetentionDays > 0,
        "GameConfig.Mail retention must be positive.")
    .Validate(c => c.Chat.MaxLength > 0 && c.Chat.RateLimitCount > 0 && c.Chat.RateLimitWindowSeconds > 0 && c.Chat.RetentionDays > 0, "GameConfig.Chat limits must be positive.")
    .Validate(c => c.Market.ListingTaxShare is >= 0 and < 1, "GameConfig.Market.ListingTaxShare must be within [0; 1) — a tax of the whole price leaves the seller nothing.")
    .Validate(c => c.Market.ListingHours > 0 && c.Market.MedianWindowHours > 0 && c.Market.ResaleCooldownHours >= 0, "GameConfig.Market hours must be positive (the resale cooldown may be zero).")
    .Validate(c => c.Market.BaseListings >= 1 && c.Market.ListingsPerBuildingLevel >= 0, "GameConfig.Market must allow at least one listing.")
    .Validate(c => c.Market.CorridorShare is > 0 and < 1, "GameConfig.Market.CorridorShare must be within (0; 1) — otherwise the corridor is empty or allows free giveaways.")
    .Validate(c => c.Market.MinSalesForMedian >= 1 && c.Market.OutlierTrimShare is >= 0 and < 0.5, "GameConfig.Market needs at least one sale for a median and must trim less than half of the sales on each side.")
    .Validate(c => c.Market.MedianMinAnchorShare is > 0 and <= 1 && c.Market.MedianMaxAnchorShare >= 1, "GameConfig.Market anchor band must contain the anchor itself: MedianMinAnchorShare ≤ 1 ≤ MedianMaxAnchorShare.")
    .Validate(c => c.Market.GoldPerGem > 0 && c.Market.GoldPerPower.Values.All(v => v > 0), "GameConfig.Market anchors must be positive.")
    .Validate(c => c.Dungeons.MaxRoundsPerWave > 0, "GameConfig.Dungeons.MaxRoundsPerWave must be positive — otherwise every battle times out on its first turn.")
    .Validate(c => c.Buildings.All(b => b.Position is null || (b.Position.X is >= 10 and <= 90 && b.Position.Y is >= 10 and <= 90)), "GameConfig has a building outside the village walls: Position must be within 10–90.")
    .ValidateOnStart();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(nameof(JwtSettings)));

// Знімки конфігу для сінглтонів — читаються тут, використовуються нижче
var gameConfig = builder.Configuration.GetSection("GameConfig").Get<GameConfig>()
    ?? throw new InvalidOperationException("GameConfig section is missing or invalid.");

var jwtSettings = builder.Configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings not configured.");

// HS256 з коротким ключем падає на першому логіні, а не на старті
if (Encoding.UTF8.GetByteCount(jwtSettings.Secret) < 32)
    throw new InvalidOperationException("JwtSettings.Secret must be at least 32 bytes for HS256.");

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
builder.Services.AddSingleton(sp => new MonsterSpawner(sp.GetRequiredService<TerrainGenerator>(), gameConfig.Map, sp.GetRequiredService<GameCatalog>(), sp.GetRequiredService<WorldGeometry>(), sp.GetRequiredService<IRandomSource>()));
builder.Services.AddSingleton(sp => new MarchCalculator(sp.GetRequiredService<TerrainGenerator>(), sp.GetRequiredService<GameCatalog>()));
builder.Services.AddSingleton(sp => new SettlementPlacer(sp.GetRequiredService<TerrainGenerator>(), sp.GetRequiredService<WorldGeometry>(), sp.GetRequiredService<IRandomSource>()));
builder.Services.AddSingleton(sp => new WorldGeometry(gameConfig.Map));
builder.Services.AddSingleton(sp => new HeroProgression(gameConfig.HeroSettings));
builder.Services.AddSingleton(sp => new HeroCombatModifiers(sp.GetRequiredService<GameCatalog>()));
builder.Services.AddSingleton(sp => new EnhancementRules(gameConfig.Equipment));
builder.Services.AddSingleton(sp => new ArtifactRoller(gameConfig.Equipment));
builder.Services.AddSingleton(sp => new BannerRoller(gameConfig.Shop));
builder.Services.AddSingleton(sp => new BattleEngine(gameConfig.Dungeons));
builder.Services.AddSingleton(sp => new BattleBuilder(gameConfig.Dungeons));
builder.Services.AddSingleton(sp => new HeroStats(sp.GetRequiredService<HeroProgression>(), sp.GetRequiredService<GameCatalog>()));
builder.Services.AddSingleton(sp => new MarketPricing(gameConfig));
builder.Services.AddSingleton<GameCatalogProjection>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<DefenceLossAllocator>();
builder.Services.AddSingleton<BattleResolver>();

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

// За reverse-proxy RemoteIpAddress — це проксі; без цього всі гравці = один IP
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Довіряти лише своєму проксі: інакше X-Forwarded-For підробляється
    foreach (var proxy in builder.Configuration.GetSection("KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Логін і реєстрація — окремо й жорстко, але ПО КЛІЄНТУ:
    // AddFixedWindowLimiter дав би один глобальний лічильник — 10 чужих спроб
    // блокували б логін усім
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Решта API — по гравцю, а за його відсутності по IP. Ліміт — від активної
    // сесії, а не від бота: збір з усіх будівель, серія роллів і кілька екранів
    // із таймерами за хвилину дають далеко за сотню запитів
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("playerId")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
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

//  Hangfire живе в тій самій PostgreSQL базі
// У тестах не піднімається взагалі: Hangfire кешує LoggerFactory у статичному
// GlobalJobFilters, а WebApplicationFactory будує хост двічі — статика від
// першого хоста переживає його disposal і падає ObjectDisposedException.
// Прибрати IHostedService недостатньо: падає резолвер IJobFilterProvider.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfire(config =>
        config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

    builder.Services.AddHangfireServer();
    builder.Services.AddHostedService<RecurringJobScheduler>();
}

// Раннер створює scope на кожен активний світ — без нього
// query-фільтри не мають що застосувати у фоновому контексті
builder.Services.AddScoped<ServerJobRunner>();
builder.Services.AddScoped<TimerScanJob>();
builder.Services.AddScoped<MonsterSpawnJob>();
builder.Services.AddScoped<OutboxMaintenanceJob>();
builder.Services.AddScoped<DailyQuestResetJob>();
builder.Services.AddScoped<ServerEvolutionJob>();
builder.Services.AddScoped<RatingRecalculationJob>();
builder.Services.AddScoped<ServerQuestTotalsJob>();
builder.Services.AddScoped<ClanLeadershipJob>();

//  8. ВЕБ-ШАР

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Кривий JSON чи невідомий enum відсікає ще прив'язка моделі, до FluentValidation.
    // Відповідь та сама, але з errorCode: клієнт розгалужується за ним, не за текстом
    var standard = options.InvalidModelStateResponseFactory;

    options.InvalidModelStateResponseFactory = context =>
    {
        var result = standard(context);

        if (result is ObjectResult { Value: ProblemDetails problem })
            problem.Extensions["errorCode"] = "Validation";

        return result;
    };
});
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

    // Стабільний operationId: із нього генератор робить ім'я методу клієнта.
    // Без нього Swashbuckle лишає порожньо, і назви залежать від маршруту
    options.CustomOperationIds(api => api.ActionDescriptor is ControllerActionDescriptor descriptor
        ? $"{descriptor.ControllerName}_{descriptor.ActionName}"
        : null);

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
    // Без цього кожен рядок у схемі позначений nullable, і типи клієнта
    // перетворюють будь-яке поле на "може не прийти"
    options.SupportNonNullableReferenceTypes();
    options.SchemaFilter<RequiredPropertiesSchemaFilter>();
});

var app = builder.Build();

//  9. КОНВЕЄР ЗАПИТУ
//  Порядок критичний: кожен наступний крок покладається на попередній.

// Найперший: далі всі бачать реальний IP клієнта, а не проксі
app.UseForwardedHeaders();

// Специфікацію віддаємо і в Testing: з неї генеруються типи фронту
// й на ній тримається тест на розходження контракту. UI лишається в dev
if (!app.Environment.IsProduction())
    app.UseSwagger();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "EmpireIdle API v1"));

    // AllowAnonymous обходить FallbackPolicy — доступ вирішує сам фільтр
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter()]
    }).AllowAnonymous();

    // Сід дев-акаунта. Поза Development маршруту не існує взагалі
    app.MapPost("/api/dev/seed/{playerId:guid}", async (Guid playerId, IMediator mediator, CancellationToken cancellationToken) =>
    {
        await mediator.Send(new SeedDevAccountCommand(playerId), cancellationToken);
        return Results.NoContent();
    }).RequireAuthorization();

    // Оголошення світу. Адмінського інструменту ще немає — публікація лише в розробці
    app.MapPost("/api/dev/announcements", async (EmpireIdle.API.DTOs.PublishAnnouncementRequest request, IMediator mediator,
        CancellationToken cancellationToken)
        => Results.Ok(await mediator.Send(new EmpireIdle.Application.Mail.Commands.PublishAnnouncementCommand(
            request.Kind, request.Title, request.Body, request.ExpiresAt), cancellationToken))).RequireAuthorization();

    // Перегенерація мапи після зміни насіння чи ваг місцевості: монстри заново, села — на придатні клітини
    app.MapPost("/api/dev/reset-map", async (IMediator mediator, IServerContext serverContext, CancellationToken cancellationToken)
        => Results.Ok(await mediator.Send(new ResetMapCommand(serverContext.ServerId), cancellationToken))).RequireAuthorization();
}
else
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseCors(FrontendCors);

app.UseAuthentication();

// ПІСЛЯ автентифікації: до неї User порожній і партиція завжди падала на IP
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();

/// <summary>Точка входу — public для WebApplicationFactory у тестах.</summary>
public partial class Program { }
