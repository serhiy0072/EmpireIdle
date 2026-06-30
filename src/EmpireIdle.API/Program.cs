using EmpireIdle.API.Hubs;
using EmpireIdle.API.Jobs;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure;
using EmpireIdle.Infrastructure.Auth;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

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
});

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

app.UseCors("FrontendCors");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHub<GameHub>("/hubs/game");

// Recurring job — тік ресурсів кожну хвилину
RecurringJob.AddOrUpdate<ResourceTickJob>(
    "resource-tick",
    job => job.RunAsync(),
    Cron.Minutely);

app.Run();

