using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Армія вдома: герой стає в гарнізон, юніти — теж, здобич — на склад.
    ///
    /// Один шлях на два приводи — звичайне повернення, яке доводить сканер,
    /// і миттєве повернення при переїзді села. Розкидані, вони розійшлися б
    /// на першій же правці: наприклад, здобич губилася б саме при телепорті.
    /// </summary>
    public sealed class MarchHomecoming
    {
        private readonly IHeroRepository _heroRepository;
        private readonly MarchLogistics _logistics;
        private readonly ILogger<MarchHomecoming> _logger;

        public MarchHomecoming(
            IHeroRepository heroRepository,
            MarchLogistics logistics,
            ILogger<MarchHomecoming> logger)
        {
            _heroRepository = heroRepository;
            _logistics = logistics;
            _logger = logger;
        }

        /// <summary>
        /// Завершує марш, що повертається, у гарнізоні <paramref name="garrison"/>.
        /// </summary>
        /// <param name="leaderSlotFree">
        /// Чи вільний слот лідера. null — спитати базу. Кілька героїв, що прибувають
        /// в одній транзакції, базу питати не можуть: незбережений лідер там ще не видно,
        /// і кожен вирішив би, що слот вільний, — тоді рахує той, хто кличе.
        /// </param>
        /// <returns>true, якщо герой маршу зайняв слот лідера.</returns>
        public async Task<bool> ArriveAsync(March march, Garrison garrison, bool? leaderSlotFree, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var tookTheLead = false;

            foreach (var hero in await _logistics.HeroesOnMarchAsync(march, cancellationToken))
            {
                // Слот могли зайняти, поки герой ішов: тоді він стає рядовим.
                // Лідер у гарнізоні один — вільний слот бере найсильніший герой маршу
                var slotFree = !tookTheLead && (leaderSlotFree
                    ?? await _heroRepository.GetLeaderAsync(garrison.Id, hero.PlayerId, cancellationToken) is null);

                hero.Arrive(garrison.Id, slotFree, utcNow);
                tookTheLead |= slotFree;
            }

            var survivors = march.GetUnits();

            if (survivors.Count > 0)
                garrison.ReceiveUnits(survivors, utcNow);

            await _logistics.UnloadCargoAsync(march, garrison, utcNow, cancellationToken);

            march.Complete(utcNow);

            _logger.LogInformation("March {MarchId} returned home with {Count} units.",
                march.Id, survivors.Values.Sum());

            return tookTheLead;
        }
    }
}
