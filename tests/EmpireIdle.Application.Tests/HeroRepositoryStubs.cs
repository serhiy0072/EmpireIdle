using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using NSubstitute;

namespace EmpireIdle.Application.Tests;

/// <summary>
/// Підміна репозиторію героїв, що відповідає на пакетні запити тим самим, що вже налаштовано
/// для одиночного GetByIdAsync, а героїв маршу шукає за MarchId (GDD §6.1): тестам досить
/// сказати, який герой за яким id, — як і раніше. Маршу без OnMarch героїв не дістається.
/// </summary>
public static class HeroRepositoryStubs
{
    public static IHeroRepository ForwardHeroLookups(this IHeroRepository heroes)
    {
        heroes.GetByIdsReadOnlyAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => Lookup(heroes, call.Arg<IReadOnlyCollection<Guid>>()));

        heroes.GetByMarchAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => new List<Hero>());

        heroes.GetByMarchesReadOnlyAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Array.Empty<Hero>().ToLookup(h => h.MarchId!.Value));

        return heroes;
    }

    /// <summary>
    /// Герої в дорозі з цим маршем — так, як їх бачить репозиторій: лише ті, чий MarchId досі
    /// вказує на марш. Хто зійшов (Arrive) посеред обробки, на наступному запиті вже не видно.
    /// </summary>
    public static void OnMarch(this IHeroRepository heroes, Guid marchId, params Hero[] onMarch)
        => heroes.GetByMarchAsync(marchId, Arg.Any<CancellationToken>())
            .Returns(_ => onMarch.Where(h => h.MarchId == marchId).ToList());

    private static List<Hero> Lookup(IHeroRepository heroes, IReadOnlyCollection<Guid> ids)
        => ids.Select(id => heroes.GetByIdAsync(id, CancellationToken.None).Result).OfType<Hero>().ToList();
}
