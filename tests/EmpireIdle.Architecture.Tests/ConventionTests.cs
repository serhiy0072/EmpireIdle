using AwesomeAssertions;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using NetArchTest.Rules;
using System.Reflection;

namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// Конвенції всередині шарів: іменування, запечатування, інкапсуляція.
/// Ці правила дешеві й ловлять дрейф, який на ревʼю пропускається.
/// </summary>
public class ConventionTests
{
    private static readonly Assembly DomainAssembly = typeof(Village).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IRepository<>).Assembly;

    [Fact]
    public void RequestHandlers_ShouldBeSealed()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(MediatR.IRequestHandler<>))
            .Or()
            .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
            .Should()
            .BeSealed()
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void RequestHandlers_ShouldHaveHandlerSuffix()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(MediatR.IRequestHandler<>))
            .Or()
            .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    /// <summary>
    /// CQRS: запит нічого не змінює. Обробник запиту з IUnitOfWork — ознака запису на GET,
    /// а такий запис конфліктує з outbox і дає гравцю 409 на звичайному перегляді.
    /// </summary>
    [Fact]
    public void QueryHandlers_ShouldNotSaveChanges()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("QueryHandler")
            .ShouldNot()
            .HaveDependencyOn(typeof(IUnitOfWork).FullName)
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Repositories_ShouldLiveInInfrastructureOnly()
    {
        // Реалізація репозиторію в Application означає, що хтось потягнув туди EF
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Repository")
            .And()
            .AreClasses()
            .Should()
            .BeAbstract()
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "в Application лишаються тільки інтерфейси репозиторіїв");
    }

    [Fact]
    public void RepositoryInterfaces_ShouldNotExposeIQueryable()
    {
        // IQueryable у контракті репозиторію означає, що запит будується
        // в хендлері — і Application знову залежить від провайдера БД
        var leaking = ApplicationAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository"))
            .SelectMany(t => t.GetMethods())
            .Where(m => m.ReturnType.Name.StartsWith("IQueryable"))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        leaking.Should().BeEmpty();
    }

    [Fact]
    public void DomainEntities_ShouldNotExposePublicSetters()
    {
        // Публічний сеттер обходить інваріанти агрегату:
        // village.Resources[0].Amount = 999_999 не має компілюватись
        var offenders = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Entity).IsAssignableFrom(t))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "стан агрегату змінюється лише його власними методами");
    }

    [Fact]
    public void DomainEntities_ShouldNotExposeMutableCollections()
    {
        // List<T> назовні дозволяє village.Buildings.Add(...) в обхід AddBuilding
        var offenders = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Entity).IsAssignableFrom(t))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.PropertyType.IsGenericType
                     && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "колекції віддаються як IReadOnlyCollection<T>");
    }

    [Fact]
    public void Commands_AndQueries_ShouldBeRecords()
    {
        // Запис MediatR має бути value-типом за семантикою: він серіалізується
        // в IdempotencyRecord і порівнюється при replay
        var messages = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                     && t.GetInterfaces().Any(i => i.IsGenericType
                         && i.GetGenericTypeDefinition() == typeof(MediatR.IRequest<>))
                     || t.GetInterfaces().Contains(typeof(MediatR.IRequest)))
            .Where(t => t.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is null)
            .Select(t => t.Name)
            .ToList();

        messages.Should().BeEmpty("команди й запити оголошуються як record");
    }

    /// <summary>
    /// Захист від IDOR: запит із PlayerId звіряє його з гравцем із токена лише через
    /// IPlayerScopedRequest. Забутий маркер — дія над чужим гравцем, і жоден інший тест не впаде.
    /// Винятки — внутрішні команди фонових задач, яких немає в контролерах.
    /// </summary>
    [Fact]
    public void RequestsWithAPlayerId_ShouldBePlayerScoped()
    {
        string[] internalOnly = ["RecalculatePlayerPowerCommand", "ResetPlayerDailyQuestsCommand"];

        var offenders = Requests()
            .Where(t => t.GetProperty("PlayerId") is not null)
            .Where(t => !typeof(Application.Common.Security.IPlayerScopedRequest).IsAssignableFrom(t))
            .Select(t => t.Name)
            .Except(internalOnly)
            .ToList();

        offenders.Should().BeEmpty("запит із PlayerId гравця реалізує IPlayerScopedRequest");
    }

    /// <summary>
    /// Команда гравця, що змінює стан, ідемпотентна: ретрай мережі не виконує її двічі.
    /// Хід данжу захищено номером ходу (expectedTurn → 409 StaleTurn), а не ключем;
    /// сід dev-акаунта існує лише в Development і повторюється свідомо.
    /// </summary>
    [Fact]
    public void PlayerCommands_ShouldBeIdempotent()
    {
        string[] guardedOtherwise = ["TakeDungeonTurnCommand", "SeedDevAccountCommand"];

        var offenders = Requests()
            .Where(t => t.Name.EndsWith("Command"))
            .Where(t => typeof(Application.Common.Security.IPlayerScopedRequest).IsAssignableFrom(t))
            .Where(t => !typeof(Application.Common.Security.IIdempotentRequest).IsAssignableFrom(t))
            .Select(t => t.Name)
            .Except(guardedOtherwise)
            .ToList();

        offenders.Should().BeEmpty("команда гравця реалізує IIdempotentRequest");
    }

    /// <summary>
    /// Без токена відкриті лише вхід, каталог і вебхук Stripe (його підпис перевіряє обробник).
    /// FallbackPolicy закриває решту — новий [AllowAnonymous] мусить потрапити сюди свідомо.
    /// </summary>
    [Fact]
    public void AnonymousAccess_ShouldStayOnTheAllowList()
    {
        string[] allowed = ["AuthController", "CatalogController", "PaymentsController.Webhook"];
        var anonymous = typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute);

        var controllers = typeof(EmpireIdle.API.Controllers.AuthController).Assembly.GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("Controller"))
            .ToList();

        var open = controllers.Where(t => t.IsDefined(anonymous, inherit: true)).Select(t => t.Name)
            .Concat(controllers
                .Where(t => !t.IsDefined(anonymous, inherit: true))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.IsDefined(anonymous, inherit: true))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}"))
            .Except(allowed)
            .ToList();

        open.Should().BeEmpty("анонімний доступ відкривається лише свідомо");
    }

    private static IEnumerable<Type> Requests()
        => ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetInterfaces().Contains(typeof(MediatR.IRequest))
                        || t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(MediatR.IRequest<>)));

    [Fact]
    public void Validators_ShouldBeSealed()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Validator")
            .And()
            .AreClasses()
            .Should()
            .BeSealed()
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }
}
