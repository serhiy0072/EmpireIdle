using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Domain.Enums;
using FluentValidation;

namespace EmpireIdle.Application.Marches.Validators
{
    public sealed class SendMarchCommandValidator : AbstractValidator<SendMarchCommand>
    {
        public SendMarchCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.TargetType).IsInEnum();
            RuleFor(x => x.TargetId).NotEmpty();
            RuleFor(x => x.Units).NotNull();
            RuleFor(x => x.Intent).IsInEnum();

            // Підкріплення може складатись із самого героя — хендлер і доставка це підтримують.
            // Атака без юнітів дала б нульову силу й гарантовану поразку
            RuleFor(x => x.Units).NotEmpty()
                .When(x => x.Intent != MarchIntent.Reinforce)
                .WithMessage("An attack needs at least one unit.");

            // Розвідники йдуть окремою командою: без героя й юнітів, зі своїми правилами
            RuleFor(x => x.Intent).NotEqual(MarchIntent.Scout)
                .WithMessage("Scouts are sent with SendScoutCommand.");

            // У монстра гарнізону немає — підкріпляти нікого. Споруду клану
            // «підкріплюють», щоб будувати й тримати гарнізон
            RuleFor(x => x.TargetType)
                .Must(t => t is MarchTargetType.Village or MarchTargetType.ClanStructure)
                .When(x => x.Intent == MarchIntent.Reinforce)
                .WithMessage("Reinforcements can only be sent to a village or a clan structure.");

            RuleForEach(x => x.Units).ChildRules(unit =>
            {
                unit.RuleFor(u => u.Key.UnitType).NotEmpty().MaximumLength(50);
                unit.RuleFor(u => u.Key.Level).GreaterThanOrEqualTo(1);
                unit.RuleFor(u => u.Value).GreaterThan(0);
            });
        }
    }
}
