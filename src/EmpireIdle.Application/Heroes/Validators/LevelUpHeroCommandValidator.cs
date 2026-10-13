using EmpireIdle.Application.Heroes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Heroes.Validators
{
    public sealed class LevelUpHeroCommandValidator : AbstractValidator<LevelUpHeroCommand>
    {
        public LevelUpHeroCommandValidator()
        {
            RuleFor(c => c.PlayerId).NotEmpty();
            RuleFor(c => c.HeroId).NotEmpty();

            // Верхня межа — від стелі рівня з запасом; точну стелю перевіряє обробник за конфігом
            RuleFor(c => c.Levels).InclusiveBetween(1, 200);
        }
    }
}
