using EmpireIdle.Application.Tower.Commands;
using EmpireIdle.Domain.Services;
using FluentValidation;

namespace EmpireIdle.Application.Tower.Validators
{
    public sealed class StormTowerFloorCommandValidator : AbstractValidator<StormTowerFloorCommand>
    {
        public StormTowerFloorCommandValidator(GameCatalog catalog)
        {
            // Розмір загону — з конфіга, той самий, що в рушії: інакше відмова й текст розходились би
            var size = catalog.Config.RealtimeBattle.SquadSize;

            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Floor).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Lanes)
                .NotEmpty()
                .Must(lanes => lanes.Count <= size && lanes.Any(id => id is not null))
                .Must(lanes => lanes.OfType<Guid>().Distinct().Count() == lanes.OfType<Guid>().Count())
                .WithMessage($"A squad needs 1–{size} distinct heroes on at most {size} lanes.");
        }
    }
}
