using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Герой гравця. Веде марш або боронить гарнізон, носить спорядження
    /// й дає бойові модифікатори.
    ///
    /// Прив'язаний до світу, а не до акаунта: gems спільні (§2.7), а армія,
    /// село й герої живуть у своєму сервері окремо.
    ///
    /// Ранг тут не зберігається — він властивість типу героя й лежить у конфігу.
    /// Тір навпаки зберігається: він змінюється еволюцією, тобто це стан
    /// екземпляра, а не довідникові дані.
    /// </summary>
    public class Hero : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>
        /// Світ, якому належить герой. Дублює ServerId гравця навмисно:
        /// query-фільтр застосовується до кореня агрегату, а не через навігацію.
        /// </summary>
        public int ServerId { get; private set; }

        /// <summary>Ключ типу героя з конфіга (наприклад "archer_lyra").</summary>
        public string HeroKey { get; private set; } = null!;

        /// <summary>Тір 1–3. Стелить рівень і множить базові стати.</summary>
        public int Tier { get; private set; }

        /// <summary>Поточний рівень. Качається чергою в залі героїв.</summary>
        public int Level { get; private set; }

        /// <summary>
        /// Сузір'я 0–6, набирається дублікатами. Дає пасивні перки
        /// стратегічного бою й рівні активних умінь для міні-гри.
        /// </summary>
        public int Constellation { get; private set; }

        public HeroState State { get; private set; }

        /// <summary>
        /// Коли герой вийде з госпіталю. Заповнене лише в стані Wounded.
        /// Місткість госпіталю героїв не обмежує — на відміну від юнітів,
        /// вони не гинуть, тож надлишку, який треба кудись подіти, не буває.
        /// </summary>
        public DateTime? HealedAt { get; private set; }

        public DateTime AcquiredAt { get; private set; }

        /// <summary>
        /// Момент останньої мутації агрегату. Змінюється навіть тоді, коли
        /// правились лише дочірні рядки — інакше токен паралелізму на корені
        /// не спрацював би, бо EF не оновив би рядок кореня.
        /// </summary>
        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        public Hero(Guid id, Guid playerId, int serverId, string heroKey, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            HeroKey = heroKey;
            Tier = 1;
            Level = 1;
            Constellation = 0;
            State = HeroState.Idle;
            AcquiredAt = utcNow;
            UpdatedAt = utcNow;
        }

        protected Hero() { } // Для EF Core

        /// <summary>Чи можна відправити героя в марш просто зараз.</summary>
        public bool IsAvailable => State == HeroState.Idle;

        /// <summary>
        /// Закріплює героя за маршем або підкріпленням.
        ///
        /// Кидає, а не повертає false: викликач уже зняв юнітів із гарнізону,
        /// і мовчазна відмова лишила б гарнізон порожнім без маршу.
        /// </summary>
        public void Deploy(DateTime utcNow)
        {
            if (State != HeroState.Idle)
                throw new InvalidStateException($"Hero {Id} is {State} and cannot be deployed.");

            State = HeroState.Deployed;
            Touch(utcNow);
        }

        /// <summary>Герой повернувся з походу неушкодженим.</summary>
        public void ReturnHome(DateTime utcNow)
        {
            if (State != HeroState.Deployed)
                throw new InvalidStateException($"Hero {Id} is not deployed.");

            State = HeroState.Idle;
            Touch(utcNow);
        }

        /// <summary>
        /// Герой лягає в госпіталь. Викликається лише при поразці: інакше
        /// будь-який бій із утратами виводив би героя з ладу, і карта
        /// зупинилась би після першої ж сутички.
        /// </summary>
        public void Wound(DateTime healedAt, DateTime utcNow)
        {
            State = HeroState.Wounded;
            HealedAt = healedAt;
            Touch(utcNow);
        }

        /// <summary>
        /// Виписує героя, якщо строк лікування вийшов. Повертає false,
        /// коли ще рано — сканер таймерів на цьому не падає.
        /// </summary>
        public bool TryHeal(DateTime utcNow)
        {
            if (State != HeroState.Wounded || HealedAt is null || HealedAt > utcNow)
                return false;

            State = HeroState.Idle;
            HealedAt = null;
            Touch(utcNow);

            return true;
        }

        /// <summary>Миттєве лікування за gems.</summary>
        public void HealInstantly(DateTime utcNow)
        {
            if (State != HeroState.Wounded)
                throw new InvalidStateException($"Hero {Id} is not wounded.");

            State = HeroState.Idle;
            HealedAt = null;
            Touch(utcNow);
        }

        /// <summary>
        /// Піднімає рівень на один. Стеля перевіряється викликачем через
        /// HeroProgression: вона залежить від ратуші й тіру, а агрегат
        /// героя ні про село, ні про конфіг не знає.
        /// </summary>
        public void GainLevel(int maxLevel, DateTime utcNow)
        {
            if (Level >= maxLevel)
                throw new RequirementNotMetException(
                    $"Hero {Id} is at its ceiling of {maxLevel}: raise the town hall or evolve the tier.");

            Level++;
            Touch(utcNow);
        }

        /// <summary>
        /// Піднімає тір. Гейт за рівнем сервера перевіряє викликач —
        /// рівень світу героєві невідомий.
        /// </summary>
        public void EvolveTier(int maxTier, DateTime utcNow)
        {
            if (Tier >= maxTier)
                throw new RequirementNotMetException($"Hero {Id} is already at the highest tier {maxTier}.");

            Tier++;
            Touch(utcNow);
        }

        /// <summary>
        /// Зараховує дублікат у сузір'я. Понад стелю дублікат не втрачається
        /// мовчки: повертає false, і викликач конвертує його за правилами гача.
        /// </summary>
        public bool TryAddConstellation(int maxConstellation, DateTime utcNow)
        {
            if (Constellation >= maxConstellation)
                return false;

            Constellation++;
            Touch(utcNow);

            return true;
        }

        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
    }
}
