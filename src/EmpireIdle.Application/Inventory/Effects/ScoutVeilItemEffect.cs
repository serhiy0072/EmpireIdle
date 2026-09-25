using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>
    /// Завіса від розвідки: ховає село гравця на строк предмета. Діюча завіса
    /// подовжується — строки складаються, а не згорають, як у сильнішого буста.
    /// </summary>
    public class ScoutVeilItemEffect : IItemEffect
    {
        public string ItemType => "scoutveil";

        private readonly IActiveEffectRepository _repository;

        public ScoutVeilItemEffect(IActiveEffectRepository repository) => _repository = repository;

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            var duration = TimeSpan.FromHours(context.Config.DurationHours * context.Count);
            var existing = await _repository.GetAsync(context.PlayerId, EffectTarget.ScoutBlock, cancellationToken);

            // Множника в завіси немає — лише строк; 1.0, щоб рядок ефекту лишався однаковим
            if (existing is null)
                await _repository.AddAsync(new ActiveEffect(Guid.NewGuid(), context.PlayerId, EffectTarget.ScoutBlock,
                    1.0, context.UtcNow, context.UtcNow + duration, context.Config.Key), cancellationToken);
            else if (existing.IsActive(context.UtcNow))
                existing.Extend(duration);
            else
                existing.Restart(1.0, context.UtcNow, context.UtcNow + duration, context.Config.Key);
        }
    }
}
