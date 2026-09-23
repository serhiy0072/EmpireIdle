using EmpireIdle.Application.Tutorial.Commands;
using EmpireIdle.Domain.Entities;
using FluentValidation;

namespace EmpireIdle.Application.Tutorial.Validators
{
    /// <summary>Ключ кроку — технічний ідентифікатор із клієнта: латиниця, цифри, крапка, дефіс, підкреслення.</summary>
    public sealed class MarkTutorialStepSeenCommandValidator : AbstractValidator<MarkTutorialStepSeenCommand>
    {
        public MarkTutorialStepSeenCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.StepKey)
                .NotEmpty()
                .MaximumLength(TutorialProgress.MaxStepKeyLength)
                .Matches("^[a-z0-9][a-z0-9._-]*$");
        }
    }
}
