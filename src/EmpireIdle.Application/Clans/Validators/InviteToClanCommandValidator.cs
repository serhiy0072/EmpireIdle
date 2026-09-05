using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    /// <summary>Запрошення самому собі не має сенсу й дало б заявку від члена клану.</summary>
    public sealed class InviteToClanCommandValidator : AbstractValidator<InviteToClanCommand>
    {
        public InviteToClanCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.TargetPlayerId).NotEmpty().NotEqual(x => x.PlayerId);
        }
    }
}
