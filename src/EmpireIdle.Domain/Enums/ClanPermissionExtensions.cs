namespace EmpireIdle.Domain.Enums
{
    public static class ClanPermissionExtensions
    {
        private static readonly ClanPermission[] Single =
            Enum.GetValues<ClanPermission>().Where(p => p != ClanPermission.None).ToArray();

        /// <summary>
        /// Прапорці окремими дозволами, у порядку оголошення. Так вони йдуть клієнту: перевірити наявність
        /// у списку простіше, ніж робити бітову арифметику, а нове право не зсуває чужих чисел.
        /// </summary>
        public static IReadOnlyList<ClanPermission> Granted(this ClanPermission permissions)
            => Single.Where(p => permissions.HasFlag(p)).ToList();
    }
}
