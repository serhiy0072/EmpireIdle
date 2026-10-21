using EmpireIdle.Application.Quests.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Quests.Validators
{
    public sealed class ClaimAllQuestRewardsCommandValidator : AbstractValidator<ClaimAllQuestRewardsCommand>
    {
        public ClaimAllQuestRewardsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
        }
    }
}
