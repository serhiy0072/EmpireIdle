using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Табір знімається й іде додому звичайним маршем (§2.5) — за відкликанням
    /// власника або після програної оборони. Одна дорога на обидва приводи:
    /// від клітинки табору до села там, де воно стоїть зараз, зі швидкістю
    /// найповільнішого в колоні, героя теж.
    /// </summary>
    public sealed class CampHomecoming
    {
        private readonly IHeroRepository _heroRepository;
        private readonly MarchCalculator _calculator;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;

        public CampHomecoming(IHeroRepository heroRepository, MarchCalculator calculator, HeroProgression progression,
            GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _calculator = calculator;
            _progression = progression;
            _catalog = catalog;
        }

        /// <returns>Скільки триватиме дорога.</returns>
        public async Task<TimeSpan> SendHomeAsync(March camp, Village home, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var hero = camp.HeroId is Guid heroId
                ? await _heroRepository.GetByIdAsync(heroId, cancellationToken)
                : null;

            var duration = _calculator.CalculateDuration(
                camp.ServerId, camp.TargetX, camp.TargetY, home.X, home.Y, camp.GetUnits(),
                hero is null ? null : _progression.MarchSpeed(_catalog.FindHero(hero.HeroKey))) / camp.SpeedMultiplier;

            camp.BreakCamp(home.X, home.Y, duration, utcNow);

            return duration;
        }
    }
}
