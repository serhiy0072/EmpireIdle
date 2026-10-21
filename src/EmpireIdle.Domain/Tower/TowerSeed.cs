namespace EmpireIdle.Domain.Tower
{
    /// <summary>
    /// Сід бою вежі — сталий для пари (гравець, поверх), дизайн §8.2: той самий загін із тими самими статами
    /// дає той самий бій, тож поверх не пройти, «ролячи кубик» ретраями, — лише прокачкою чи іншим складом.
    /// Рахується з байтів Guid, а не з GetHashCode: той рандомізований між процесами.
    /// </summary>
    public static class TowerSeed
    {
        public static int For(Guid playerId, int floor)
        {
            var bytes = playerId.ToByteArray();
            var hash = BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 4)
                ^ BitConverter.ToInt32(bytes, 8) ^ BitConverter.ToInt32(bytes, 12);

            return unchecked(hash * 31 + floor);
        }
    }
}
