using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Domain.Entities;
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
            RuleFor(x => x.Units).NotEmpty();
            RuleFor(x => x.Intent).IsInEnum();

            // У монстра гарнізону немає — підкріпляти нікого
            RuleFor(x => x.TargetType)
                .Equal(MarchTargetType.Village)
                .When(x => x.Intent == MarchIntent.Reinforce)
                .WithMessage("Reinforcements can only be sent to a village.");

            RuleForEach(x => x.Units).ChildRules(unit =>
            {
                unit.RuleFor(u => u.Key).NotEmpty().MaximumLength(50);
                unit.RuleFor(u => u.Value).GreaterThan(0);
            });
        }
    }
}
