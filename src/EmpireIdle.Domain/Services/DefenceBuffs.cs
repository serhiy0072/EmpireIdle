namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Бонуси всіх лідерів, що стоять в одному гарнізоні.
    ///
    /// Ключ — власник стека, а не гарнізон: у моєму селі стоять
    /// підкріплення кількох союзників, і кожен стек воює під бонусом
    /// свого лідера, не мого.
    /// </summary>
    public sealed class DefenceBuffs
    {
        /// <summary>Жодного лідера: усі множники 1.0.</summary>
        public static readonly DefenceBuffs None = new(StackBuff.None, new());

        private readonly StackBuff _host;
        private readonly Dictionary<Guid, StackBuff> _allies;

        public DefenceBuffs(StackBuff host, Dictionary<Guid, StackBuff> allies)
        {
            _host = host;
            _allies = allies;
        }

        /// <summary>
        /// Бонус для стека цього власника. Порожній власник означає
        /// юнітів господаря: DefenceStack так і позначає свої війська.
        /// </summary>
        public StackBuff For(Guid? ownerPlayerId)
            => ownerPlayerId is { } owner
            ? _allies.GetValueOrDefault(owner, StackBuff.None)
            : _host;
    }
}
