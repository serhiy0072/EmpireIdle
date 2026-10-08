using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Піднімає вміння героя на рівень за одну книгу (GDD §6.1, рішення 08.10.2026). Книга — своєї
    /// ролі, рідкості й половини; вміння мусить бути відкрите рівнем героя, а наступний рівень —
    /// зірками: на N зірках доступний рівень N + 1.
    /// </summary>
    public record UpgradeHeroSkillCommand(Guid PlayerId, Guid HeroId, string SkillKey)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class UpgradeHeroSkillCommandHandler : IRequestHandler<UpgradeHeroSkillCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HeroSkills _skills;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UpgradeHeroSkillCommandHandler> _logger;

        public UpgradeHeroSkillCommandHandler(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            IUnitOfWork unitOfWork,
            HeroSkills skills,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<UpgradeHeroSkillCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _unitOfWork = unitOfWork;
            _skills = skills;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(UpgradeHeroSkillCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Лот продається за силу на момент виставлення
            if (hero.State == HeroState.OnMarket)
                throw new InvalidStateException(RefusalReasons.HeroOnMarket, $"Hero {hero.Id} is on the market.");

            var config = _catalog.FindHero(hero.HeroKey)
                ?? throw new EntityNotFoundException("Hero config", hero.HeroKey);

            var skill = config.Skills.FirstOrDefault(s => s.Key == request.SkillKey)
                ?? throw new EntityNotFoundException("Hero skill", request.SkillKey);

            if (!HeroSkills.IsUnlocked(hero, skill))
                throw new RequirementNotMetException(RefusalReasons.HeroSkillLocked,
                    $"Skill '{skill.Key}' unlocks at hero level {skill.UnlockLevel}.", skill.DisplayName, skill.UnlockLevel);

            // Стелі перевіряються до списання книги: інакше відмова з'їла б її
            var settings = _catalog.Config.HeroSettings;
            var current = hero.SkillLevel(skill.Key);

            if (current >= settings.MaxSkillLevel)
                throw new RequirementNotMetException(RefusalReasons.HeroSkillMaxed,
                    $"Skill '{skill.Key}' is already at the top level.", settings.MaxSkillLevel);

            if (current >= _skills.LevelCap(hero))
                throw new RequirementNotMetException(RefusalReasons.HeroSkillStarCapped,
                    $"Skill '{skill.Key}' needs {current} stars for level {current + 1}.", current);

            var book = _skills.BookFor(config, skill.Half)
                ?? throw new InvalidOperationException($"No skill book for {config.Class}/{config.Rank}/{skill.Half}.");

            var bookName = _catalog.FindItem(book.ItemKey)?.DisplayName ?? book.ItemKey;
            var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, book.ItemKey, cancellationToken);

            if (stack is null || stack.Count < 1)
                throw new RequirementNotMetException(RefusalReasons.HeroSkillNoBook,
                    $"Raising '{skill.Key}' needs a '{book.ItemKey}'.", bookName);

            stack.Consume(1);
            hero.RaiseSkill(skill.Key, _skills.LevelCap(hero), settings.MaxSkillLevel, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} raised skill {SkillKey} to level {Level} with {Book}",
                hero.Id, skill.Key, hero.SkillLevel(skill.Key), book.ItemKey);
        }
    }
}
