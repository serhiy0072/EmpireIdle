using FluentValidation;

namespace EmpireIdle.Application.Marches.Commands.Validators
{
    public sealed class SendMarchCommandValidator : AbstractValidator<SendMarchCommand>
    {
        public SendMarchCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.TargetType).IsInEnum();
            RuleFor(x => x.TargetId).NotEmpty();
            RuleFor(x => x.Units).NotEmpty();

            RuleForEach(x => x.Units).ChildRules(unit =>
            {
                unit.RuleFor(u => u.Key).NotEmpty().MaximumLength(50);
                unit.RuleFor(u => u.Value).GreaterThan(0);
            });
        }
    }
}
