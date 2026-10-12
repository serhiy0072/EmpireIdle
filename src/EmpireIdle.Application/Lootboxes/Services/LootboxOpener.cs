using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Lootboxes.Services
{
    /// <summary>
    /// Відкриває скрині (GDD §9.12): кидок таблиці з гарантією, розкриття вкладених скринь
    /// і випадкових артефактів, видача через диспетчер нагород. Звідки скриня — з рюкзака
    /// чи з крамниці — вирішує команда; збереження теж за нею.
    /// </summary>
    public sealed class LootboxOpener
    {
        private readonly ILootboxRepository _lootboxes;
        private readonly IVillageRepository _villages;
        private readonly RewardDispatcher _dispatcher;
        private readonly GameCatalog _catalog;
        private readonly IRandomSource _random;

        public LootboxOpener(
            ILootboxRepository lootboxes,
            IVillageRepository villages,
            RewardDispatcher dispatcher,
            GameCatalog catalog,
            IRandomSource random)
        {
            _lootboxes = lootboxes;
            _villages = villages;
            _dispatcher = dispatcher;
            _catalog = catalog;
            _random = random;
        }

        /// <summary>Відкриває count скринь поспіль; кожна бачить лічильник гарантії після попередньої.</summary>
        public async Task<IReadOnlyList<LootboxDrop>> OpenAsync(Guid playerId, LootboxConfig box, int count,
            string reference, DateTime utcNow, CancellationToken cancellationToken)
        {
            var session = new Session(playerId, reference, utcNow);

            for (var i = 0; i < count; i++)
                await OpenOneAsync(session, box, cancellationToken);

            return session.Drops;
        }

        /// <summary>Одне відкриття — Rolls кидків таблиці поспіль.</summary>
        private async Task OpenOneAsync(Session session, LootboxConfig box, CancellationToken cancellationToken)
        {
            for (var i = 0; i < box.Rolls; i++)
                await RollOnceAsync(session, box, cancellationToken);
        }

        private async Task RollOnceAsync(Session session, LootboxConfig box, CancellationToken cancellationToken)
        {
            var pity = await PityAsync(session, box.Key, cancellationToken);

            // Сід окремо від кидка — так само, як у банера
            var roll = LootboxRoller.Roll(box, pity.Since, _random.Next(int.MaxValue));
            pity.Apply(roll.PitySince);

            var entry = roll.Entry;

            if (entry.IsLootbox)
            {
                // Валідатор гарантує ключ і відсутність циклу
                var nested = _catalog.Lootboxes[entry.Key!];

                for (var i = 0; i < entry.Amount; i++)
                    await OpenOneAsync(session, nested, cancellationToken);

                return;
            }

            if (entry.IsArtifact)
            {
                var rarity = Enum.Parse<Rarity>(entry.Key!, ignoreCase: true);
                var pieces = await ArtifactPiecesAsync(session, rarity, cancellationToken);

                // Кожен артефакт — окремий кидок предмета: два з однієї позиції не мусять бути однаковими
                for (var i = 0; i < entry.Amount; i++)
                {
                    var piece = pieces[_random.Next(pieces.Count)];
                    await GrantAsync(session, box, new RewardConfig { Type = "Equipment", Key = piece, Amount = 1 }, roll.WasPity,
                        cancellationToken);
                }

                return;
            }

            await GrantAsync(session, box, new RewardConfig { Type = entry.Type, Key = entry.Key, Amount = entry.Amount }, roll.WasPity,
                cancellationToken);
        }

        private async Task GrantAsync(Session session, LootboxConfig box, RewardConfig reward, bool wasPity,
            CancellationToken cancellationToken)
        {
            await _dispatcher.GrantAllAsync(session.PlayerId, [reward], session.Reference, session.UtcNow, cancellationToken);
            session.Drops.Add(new LootboxDrop(reward.Type, reward.Key, reward.Amount, box.Key, wasPity));
        }

        /// <summary>Лічильник з кешу сесії: вкладена скриня може відкритися кілька разів за один запит.</summary>
        private async Task<LootboxPity> PityAsync(Session session, string key, CancellationToken cancellationToken)
        {
            if (session.Pities.TryGetValue(key, out var cached))
                return cached;

            var pity = await _lootboxes.GetPityAsync(session.PlayerId, key, cancellationToken);

            if (pity is null)
            {
                pity = new LootboxPity(Guid.NewGuid(), session.PlayerId, key);
                await _lootboxes.AddPityAsync(pity, cancellationToken);
            }

            session.Pities[key] = pity;
            return pity;
        }

        /// <summary>
        /// Предмети наборів заданої рідкості з данжів, уже відкритих гравцеві за ратушею. Жоден ще
        /// не відкритий — набір найпершого данжу: скриня не має видати порожнечу новачкові.
        /// </summary>
        private async Task<IReadOnlyList<string>> ArtifactPiecesAsync(Session session, Rarity rarity,
            CancellationToken cancellationToken)
        {
            if (session.Pieces.TryGetValue(rarity, out var cached))
                return cached;

            if (session.MainLevel is null)
            {
                var village = await _villages.GetByPlayerIdReadOnlyAsync(session.PlayerId, cancellationToken)
                    ?? throw new EntityNotFoundException("Village for player", session.PlayerId);

                session.MainLevel = village.Buildings.FirstOrDefault(b => b.Type == _catalog.MainBuildingKey)?.Level.Value ?? 0;
            }

            var suffix = rarity.ToString().ToLowerInvariant();

            // Валідатор гарантує, що бодай один данж має набір цієї рідкості
            var dungeons = _catalog.Dungeons.Values
                .Where(d => _catalog.SetPieces.ContainsKey($"{d.ArtifactSetKey}_{suffix}"))
                .ToList();

            var open = dungeons.Where(d => d.RequiresMainBuildingLevel <= session.MainLevel).ToList();

            if (open.Count == 0)
            {
                var first = dungeons.Min(d => d.RequiresMainBuildingLevel);
                open = dungeons.Where(d => d.RequiresMainBuildingLevel == first).ToList();
            }

            // Сортування — щоб порядок у JSON не впливав на те, що дає той самий кидок
            var pieces = open
                .SelectMany(d => _catalog.SetPieces[$"{d.ArtifactSetKey}_{suffix}"])
                .Distinct()
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            session.Pieces[rarity] = pieces;
            return pieces;
        }

        /// <summary>Стан одного запиту: випади, кеш лічильників і пулів артефактів.</summary>
        private sealed class Session(Guid playerId, string reference, DateTime utcNow)
        {
            public Guid PlayerId { get; } = playerId;
            public string Reference { get; } = reference;
            public DateTime UtcNow { get; } = utcNow;
            public List<LootboxDrop> Drops { get; } = [];
            public Dictionary<string, LootboxPity> Pities { get; } = [];
            public Dictionary<Rarity, IReadOnlyList<string>> Pieces { get; } = [];
            public int? MainLevel { get; set; }
        }
    }
}
