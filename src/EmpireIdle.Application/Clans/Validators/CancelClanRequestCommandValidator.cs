using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    public sealed class CancelClanRequestCommandValidator : AbstractValidator<CancelClanRequestCommand>
    {
        public CancelClanRequestCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.RequestId).NotEmpty();
        }
    }
}
