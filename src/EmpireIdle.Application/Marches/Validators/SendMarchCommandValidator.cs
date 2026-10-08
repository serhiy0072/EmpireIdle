using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
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

            // До трьох героїв різних ролей (GDD §6.1): роль сервер звіряє з конфігом, тут — лише форма
            RuleFor(x => x.HeroIds).NotEmpty()
                .Must(ids => ids.Count <= HeroConvoys.MaxHeroesPerMarch).WithMessage($"A march takes at most {HeroConvoys.MaxHeroesPerMarch} heroes.")
                .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("A hero cannot lead the same march twice.");
            RuleForEach(x => x.HeroIds).NotEmpty();
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

            // Приручають лише монстрів (GDD §5.10)
            RuleFor(x => x.TargetType).Equal(MarchTargetType.Monster)
                .When(x => x.Intent == MarchIntent.Tame)
                .WithMessage("Only monsters can be tamed.");

            RuleForEach(x => x.Units).ChildRules(unit =>
            {
                unit.RuleFor(u => u.Key.UnitType).NotEmpty().MaximumLength(50);
                unit.RuleFor(u => u.Key.Level).GreaterThanOrEqualTo(1);
                unit.RuleFor(u => u.Value).GreaterThan(0);
            });
        }
    }
}
