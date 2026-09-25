using EmpireIdle.Application.Scouting.Commands;
using EmpireIdle.Domain.Enums;
using FluentValidation;

namespace EmpireIdle.Application.Scouting.Validators
{
    public sealed class SendScoutCommandValidator : AbstractValidator<SendScoutCommand>
    {
        public SendScoutCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.TargetId).NotEmpty();

            // Монстра розвідувати нема чого: його склад і так видно на клітині
            RuleFor(x => x.TargetType)
                .Must(t => t is MarchTargetType.Village or MarchTargetType.ClanStructure)
                .WithMessage("Only a village or a clan structure can be scouted.");
        }
    }
}
