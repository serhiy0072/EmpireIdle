using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Правила кланової території (GDD §7.2), виведені з конфіга: скільки слотів
    /// відкрито, чи клітина під покриттям, який бонус і скільки прискорює марш.
    /// Стан не зберігається — інакше розійшовся б із конфігом на першому ребалансі.
    /// </summary>
    public sealed class ClanTerritoryRules
    {
        private readonly ClanTerritoryConfig _config;

        public ClanTerritoryRules(GameCatalog catalog) => _config = catalog.Config.Clan.Territory;

        public bool Enabled => _config.Enabled;

        public int Radius => _config.Radius;

        /// <summary>Скільки очок вкладу коштує закласти споруду.</summary>
        public long StructureCost => _config.StructureCostPoints;

        /// <summary>Скільки будується споруда без допомоги маршів.</summary>
        public TimeSpan BuildDuration => TimeSpan.FromMinutes(_config.BuildMinutes);

        /// <summary>Скільки юнітів вміщає гарнізон споруди.</summary>
        public int GarrisonCapacity => _config.GarrisonCapacity;

        /// <summary>Стеля сумарного прискорення будівництва маршами.</summary>
        public double MaxBuildShare => _config.MaxBuildShare;

        /// <summary>
        /// Скільки слотів відкрито клану: стартові плюс кожна виконана умова —
        /// досягнутий поріг учасників або завершений квест клану.
        /// </summary>
        public int SlotsFor(int memberCount, IReadOnlyCollection<string> completedClanQuests)
        {
            var unlocked = _config.SlotUnlocks.Count(unlock =>
                (unlock.MinMembers is { } min && memberCount >= min)
                || (unlock.QuestKey is { } key && completedClanQuests.Contains(key)));

            return Math.Min(_config.MaxStructures, _config.StartingSlots + unlocked);
        }

        /// <summary>
        /// Чи клітина під покриттям хоч однієї добудованої споруди. Кількість споруд
        /// розширює покриття, а не бонус: перекриття не складаються.
        /// </summary>
        public bool IsCovered(IEnumerable<ClanStructure> structures, int x, int y, DateTime utcNow)
            => _config.Enabled && structures.Any(s => s.IsActiveAt(utcNow) && s.Covers(x, y, _config.Radius));

        /// <summary>Множник атаки для військ клану в радіусі; поза ним — 1.</summary>
        public double AttackMultiplier(bool covered) => covered ? 1 + _config.AttackBonus : 1;

        /// <summary>Множник оборони для військ клану в радіусі; поза ним — 1.</summary>
        public double DefenceMultiplier(bool covered) => covered ? 1 + _config.DefenceBonus : 1;

        /// <summary>
        /// Частка повного будівництва, яку дає марш: пропорційна його силі,
        /// тож один сильний марш прискорює більше за кілька слабких.
        /// </summary>
        public double BuildShareFor(double marchPower) => Math.Max(0, marchPower) * _config.BuildSharePerPower;

        /// <summary>Очки вкладу за перемогу над монстром: частка здобичі, нагорода гравця не зменшується.</summary>
        public long CashbackFor(IReadOnlyDictionary<string, int> loot)
            => (long)Math.Floor(loot.Values.Where(v => v > 0).Sum(v => (long)v) * _config.MonsterLootCashbackShare);
    }
}
