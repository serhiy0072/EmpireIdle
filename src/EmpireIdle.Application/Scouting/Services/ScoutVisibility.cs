using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Scouting.Services
{
    /// <summary>
    /// Чи сховалась ціль від розвідки. Завіса — предмет гравця, тож діє лише на його село;
    /// споруда клану власника-гравця не має і сховатись не може.
    /// </summary>
    public sealed class ScoutVisibility
    {
        private readonly IActiveEffectRepository _effects;

        public ScoutVisibility(IActiveEffectRepository effects) => _effects = effects;

        public async Task<bool> IsHiddenAsync(MarchTarget target, DateTime utcNow, CancellationToken cancellationToken)
        {
            if (target.Village is null)
                return false;

            var veil = await _effects.GetAsync(target.Village.PlayerId, EffectTarget.ScoutBlock, cancellationToken);

            return veil is not null && veil.IsActive(utcNow);
        }
    }
}
