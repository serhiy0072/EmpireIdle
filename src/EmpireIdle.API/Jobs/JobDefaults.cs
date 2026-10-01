namespace EmpireIdle.API.Jobs
{
    /// <summary>Спільні налаштування повторюваних джобів Hangfire.</summary>
    public static class JobDefaults
    {
        /// <summary>
        /// Скільки чекати лок попереднього прогону. Довгий очікувач займав би воркер і після
        /// таймауту ще й ретраївся б; повторюваний джоб і так прийде знову за розкладом,
        /// тож краще швидко здатися.
        /// </summary>
        public const int LockWaitSeconds = 10;
    }
}
