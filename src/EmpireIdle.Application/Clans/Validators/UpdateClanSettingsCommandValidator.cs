using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    /// <summary>Межа опису — колонка Clans.Description(512).</summary>
    public sealed class UpdateClanSettingsCommandValidator : AbstractValidator<UpdateClanSettingsCommand>
    {
        public UpdateClanSettingsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Description).NotNull().MaximumLength(512);
            RuleFor(x => x.JoinPolicy).IsInEnum();
        }
    }
}
