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
        private readonly MarchLogistics _logistics;
        private readonly ILogger<MarchHomecoming> _logger;

        public MarchHomecoming(
            MarchLogistics logistics,
            ILogger<MarchHomecoming> logger)
        {
            _logistics = logistics;
            _logger = logger;
        }

        /// <summary>
        /// Завершує марш, що повертається, у гарнізоні <paramref name="garrison"/>.
        /// </summary>
        public async Task ArriveAsync(March march, Garrison garrison, DateTime utcNow, CancellationToken cancellationToken)
        {
            foreach (var hero in await _logistics.HeroesOnMarchAsync(march, cancellationToken))
                hero.Arrive(garrison.Id, utcNow);

            var survivors = march.GetUnits();

            if (survivors.Count > 0)
                garrison.ReceiveUnits(survivors, utcNow);

            await _logistics.UnloadCargoAsync(march, garrison, utcNow, cancellationToken);

            march.Complete(utcNow);

            _logger.LogInformation("March {MarchId} returned home with {Count} units.",
                march.Id, survivors.Values.Sum());
        }
    }
}
