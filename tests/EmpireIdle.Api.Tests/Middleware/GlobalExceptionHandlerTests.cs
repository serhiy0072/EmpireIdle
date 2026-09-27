using System.Text.Json;
using EmpireIdle.API.Middleware;
using EmpireIdle.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmpireIdle.Api.Tests.Middleware;

/// <summary>
/// Контракт помилок для клієнта: статус, errorCode і цифри нестачі.
/// Фронт розгалужується за errorCode, тож його зміна ламає інтерфейс.
/// </summary>
public class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        return (context.Response.StatusCode, document.RootElement.Clone());
    }

    [Fact]
    public async Task Handle_ShouldReportTheShortfall_WithNumbers()
    {
        var (status, body) = await HandleAsync(new NotEnoughResourcesException("gems", need: 150, have: 100));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("NotEnoughResources", body.GetProperty("errorCode").GetString());
        Assert.Equal("gems", body.GetProperty("resource").GetString());
        Assert.Equal(150, body.GetProperty("need").GetInt64());
        Assert.Equal(100, body.GetProperty("have").GetInt64());
    }

    [Fact]
    public async Task Handle_ShouldMapNotFound()
    {
        var (status, body) = await HandleAsync(new EntityNotFoundException("Banner", "no_such_banner"));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal("NotFound", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Handle_ShouldMapConcurrencyToConflict()
    {
        var (status, body) = await HandleAsync(new DbUpdateConcurrencyException());

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("ConcurrencyConflict", body.GetProperty("errorCode").GetString());
    }

    /// <summary>На 500 назовні не йде ні тип винятку, ні його повідомлення.</summary>
    [Fact]
    public async Task Handle_ShouldHideTheDetails_OnAnUnexpectedFailure()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("connection string: host=secret"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("Internal", body.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("secret", body.GetProperty("detail").GetString());
    }

    /// <summary>Клієнт показує гравцю текст за reason, підставляючи args, — Detail лишається для консолі.</summary>
    [Fact]
    public async Task Handle_ShouldPassTheRefusalReasonWithArgs()
    {
        var reason = new RefusalReason("test.levelLocked", "level");

        var (status, body) = await HandleAsync(new RequirementNotMetException(reason, "Requires town hall 5.", 5));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("RequirementNotMet", body.GetProperty("errorCode").GetString());
        Assert.Equal("test.levelLocked", body.GetProperty("reason").GetString());
        Assert.Equal(5, body.GetProperty("args").GetProperty("level").GetInt32());
    }

    [Fact]
    public async Task Handle_ShouldOmitTheReason_ForARefusalWithoutOne()
    {
        var (_, body) = await HandleAsync(new RequirementNotMetException("Weapon 'x' has no price."));

        Assert.False(body.TryGetProperty("reason", out _));
        Assert.False(body.TryGetProperty("args", out _));
    }

    /// <summary>Захисна перевірка коду спрацювала — це баг сервера, а не хибний запит гравця.</summary>
    [Theory]
    [InlineData(typeof(ArgumentOutOfRangeException))]
    [InlineData(typeof(ArgumentNullException))]
    public async Task Handle_ShouldTreatAGuardClause_AsAServerBug(Type exceptionType)
    {
        var (status, body) = await HandleAsync((Exception)Activator.CreateInstance(exceptionType, "amount")!);

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("Internal", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Звичайний ArgumentException — хибний вхід: лишається 400.</summary>
    [Fact]
    public async Task Handle_ShouldKeepAPlainArgumentException_AsBadRequest()
    {
        var (status, body) = await HandleAsync(new ArgumentException("Unknown resource 'x'."));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("InvalidArgument", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Клієнт пішов — 499 без тіла, а не 500 з LogError.</summary>
    [Fact]
    public async Task Handle_ShouldAnswer499_WhenTheClientAborts()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();

        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, new OperationCanceledException(aborted.Token), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    /// <summary>Скасування без обриву запиту (наш власний таймаут) — справжній збій, 500.</summary>
    [Fact]
    public async Task Handle_ShouldTreatAnInternalCancellation_AsAFailure()
    {
        var (status, _) = await HandleAsync(new OperationCanceledException());

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
    }

    [Fact]
    public async Task Handle_ShouldAlwaysCarryATraceId()
    {
        var (_, body) = await HandleAsync(new EntityNotFoundException("Hero", Guid.NewGuid()));

        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }
}
