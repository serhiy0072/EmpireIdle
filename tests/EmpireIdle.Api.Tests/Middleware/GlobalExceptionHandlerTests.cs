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

    [Fact]
    public async Task Handle_ShouldAlwaysCarryATraceId()
    {
        var (_, body) = await HandleAsync(new EntityNotFoundException("Hero", Guid.NewGuid()));

        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }
}
