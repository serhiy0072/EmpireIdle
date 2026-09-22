using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
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

        public DateTime AcquiredAt { get; private set; }

        /// <summary>
        /// Гарнізон, у якому герой зараз стоїть. Порожнє означає «в дорозі»:
        /// на час маршу героя немає ні вдома, ні в чужому селі, як і юнітів.
        ///
        /// Своє село й чуже тут не розрізняються навмисно: підкріплення —
        /// той самий випадок, і єдине правило замість двох знімає спецвипадок
        /// «герой господаря» з бойової формули.
        /// </summary>
        public Guid? StationedGarrisonId { get; private set; }

        /// <summary>
        /// Лідер гарнізону. Лише він дає бонус своїм стекам і лише він
        /// потрапляє в госпіталь при поразці — інакше зібраний ростер
        /// множив би оборону, а одна програна битва клала б увесь зал.
        ///
        /// Слот один на гравця в гарнізоні, і стежить за цим частковий
        /// унікальний індекс, а не перевірка в хендлері.
        /// </summary>
        public bool IsLeader { get; private set; }

        /// <summary>
        /// Момент останньої мутації агрегату. Змінюється навіть тоді, коли
        /// правились лише дочірні рядки — інакше токен паралелізму на корені
        /// не спрацював би, бо EF не оновив би рядок кореня.
        /// </summary>
        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        public Hero(Guid id, Guid playerId, int serverId, string heroKey, Guid garrisonId, bool asLeader,
            DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            HeroKey = heroKey;
            Tier = 1;
            Level = 1;
            Constellation = 0;
            State = HeroState.Idle;
            StationedGarrisonId = garrisonId;
            IsLeader = asLeader;
            AcquiredAt = utcNow;
            UpdatedAt = utcNow;

            RaiseDomainEvent(new HeroChanged(playerId, id, utcNow));
        }

        protected Hero() { } // Для EF Core

        /// <summary>Чи можна відправити героя в марш просто зараз.</summary>
        public bool IsAvailable => State == HeroState.Idle;

        /// <summary>
        /// Знімає героя з гарнізону в марш або підкріплення.
        ///
        /// Кидає, а не повертає false: викликач уже зняв юнітів із гарнізону,
        /// і мовчазна відмова лишила б гарнізон порожнім без маршу.
        ///
        /// Лідерство складається тут же. Гарнізон лишається без лідера,
        /// поки герой не повернеться або поки не призначать іншого.
        /// </summary>
        public void Deploy(DateTime utcNow)
        {
            if (State != HeroState.Idle)
                throw new InvalidStateException($"Hero {Id} is {State} and cannot be deployed.");

            State = HeroState.Deployed;
            StationedGarrisonId = null;
            IsLeader = false;
            Touch(utcNow);
        }

        /// <summary>
        /// Похід закінчився, герой став у гарнізон. Свій він чи чужий,
        /// герою байдуже: підкріплення лишається стояти в союзника,
        /// атака повертає його додому, перехід той самий.
        ///
        /// «У дорозі» визначає порожній гарнізон, а не стан: поранений
        /// у поході теж їде й теж прибуває, лишаючись пораненим.
        /// </summary>
        /// <param name="leaderSlotFree">
        /// Чи вільний лідерський слот саме зараз. Якщо за час походу
        /// призначили іншого, герой стає рядовим: інакше на SaveChanges
        /// прилетіло б порушення індексу гравцеві, який нічого не робив.
        /// </param>
        public void Arrive(Guid garrisonId, bool leaderSlotFree, DateTime utcNow)
        {
            // У дорозі означає порожній гарнізон: іншого способу його втратити,
            // ніж Deploy або SendHome, у героя немає — конструктор вимагає гарнізон
            if (StationedGarrisonId is not null)
                throw new InvalidStateException($"Hero {Id} is not on the move.");

            if (State == HeroState.Deployed)
                State = HeroState.Idle;

            StationedGarrisonId = garrisonId;
            IsLeader = leaderSlotFree;
            Touch(utcNow);
        }

        /// <summary>
        /// Знімає героя з гарнізону в дорогу додому. На відміну від Deploy
        /// дозволений і пораненому: поранений лідер їде зі своїм загоном,
        /// а лікувати його можна лише у власному госпіталі.
        /// </summary>
        public void SendHome(DateTime utcNow)
        {
            if (StationedGarrisonId is null)
                throw new InvalidStateException($"Hero {Id} is already on the move.");

            if (State == HeroState.Idle)
                State = HeroState.Deployed;

            StationedGarrisonId = null;
            IsLeader = false;
            Touch(utcNow);
        }

        /// <summary>
        /// Ставить героя в гарнізон, не змінюючи стану. Використовується
        /// видачею й переведенням між гарнізонами.
        /// </summary>
        public void StationIn(Guid garrisonId, bool asLeader, DateTime utcNow)
        {
            if (State == HeroState.Deployed)
                throw new InvalidStateException($"Hero {Id} is on the move and cannot be stationed.");

            StationedGarrisonId = garrisonId;
            IsLeader = asLeader;
            Touch(utcNow);
        }

        /// <summary>
        /// Призначає лідером гарнізону, у якому герой уже стоїть.
        /// Поранений лідером бути може: бонус дає той, хто в строю,
        /// а перевірка стану — справа бойової формули, не призначення.
        /// </summary>
        public void AppointLeader(DateTime utcNow)
        {
            if (StationedGarrisonId is null)
                throw new InvalidStateException($"Hero {Id} is not stationed anywhere.");

            IsLeader = true;
            Touch(utcNow);
        }

        /// <summary>Складає лідерство, лишаючись у гарнізоні.</summary>
        public void DismissLeader(DateTime utcNow)
        {
            IsLeader = false;
            Touch(utcNow);
        }

        /// <summary>
        /// Герой лягає в госпіталь. Викликається лише при поразці: інакше
        /// будь-який бій із утратами виводив би героя з ладу, і карта
        /// зупинилась би після першої ж сутички.
        ///
        /// Ідемпотентний: розбір бою може зачепити того самого лідера двічі,
        /// і другий раз не має бути помилкою.
        ///
        /// Лідерство лишається за ним. Слот звільняє або лікування, або
        /// явне призначення іншого — мовчазна втрата посади після поразки
        /// виглядала б як баг.
        /// </summary>
        public void Wound(DateTime utcNow)
        {
            if (State == HeroState.Wounded)
                return;

            State = HeroState.Wounded;
            Touch(utcNow);
        }

        /// <summary>
        /// Виписує героя з госпіталю. Лікування миттєве, як і в юнітів:
        /// ціну платять ресурсами, а не часом — списує викликач.
        /// </summary>
        public void Heal(DateTime utcNow)
        {
            if (State != HeroState.Wounded)
                throw new InvalidStateException($"Hero {Id} is not wounded.");

            if (StationedGarrisonId is null)
                throw new InvalidStateException($"Hero {Id} is on the move and cannot be healed.");

            State = HeroState.Idle;
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
            RaiseDomainEvent(new HeroChanged(PlayerId, Id, utcNow));
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
            RaiseDomainEvent(new HeroChanged(PlayerId, Id, utcNow));
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
            RaiseDomainEvent(new HeroChanged(PlayerId, Id, utcNow));

            return true;
        }

        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
    }
}
