using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>
    /// Формула рейтингу: Power домінує, але має стелю. Саме обрізання робить
    /// конструкцію саморегулівною — хто вичерпав бойову вісь, змагається
    /// вкладеннями. Тому тести на кламп тут важливіші за тести на арифметику.
    /// </summary>
    public class PlayerRatingTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Круглі числа замість бойових: 100 сили = 100% ваги, тож очікування
        /// в тестах читаються як частки, а не як результат ділення.
        /// </summary>
        private static RatingConfig Config() => new()
        {
            PowerWeight = 0.55,
            DevelopmentWeight = 0.25,
            ActivityWeight = 0.20,
            PowerReference = 100,
            DevelopmentReference = 100,
            ActivityReference = 100,
            Scale = 10_000,
            PointsPerMonster = 5,
            PointsPerBattleWon = 10,
            PointsPerQuest = 20,
            PointsPerContribution = 1
        };

        private static PlayerRating NewRating() => new(Guid.NewGuid(), Guid.NewGuid(), 1, Now);

        /// <summary>Порожній гравець має нульовий рейтинг, а не помилку.</summary>
        [Fact]
        public void Recalculate_ShouldBeZero_ForANewPlayer()
        {
            var rating = NewRating();

            rating.Recalculate(power: 0, buildingLevelSum: 0, Config(), Now);

            Assert.Equal(0, rating.TotalRating);
        }

        /// <summary>Вичерпані всі три осі дають рівно Scale — стеля передбачувана.</summary>
        [Fact]
        public void Recalculate_ShouldReachExactlyScale_WhenEveryAxisIsMaxed()
        {
            var rating = NewRating();
            rating.RecordActivity(quests: 5); // 5 × 20 = 100 очок

            rating.Recalculate(power: 100, buildingLevelSum: 100, Config(), Now);

            Assert.Equal(10_000, rating.TotalRating);
        }

        /// <summary>
        /// Сила понад орієнтир не додає нічого. Це і є стеля: подвоєння армії
        /// після вичерпання осі не рухає топ, і гравець мусить рости іншим.
        /// </summary>
        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(100_000)]
        public void Recalculate_ShouldCapPowerAtTheReference(double power)
        {
            var rating = NewRating();

            rating.Recalculate(power, buildingLevelSum: 0, Config(), Now);

            // 0.55 × 10 000
            Assert.Equal(5_500, rating.TotalRating);
        }

        /// <summary>Power важить більше за решту разом — топ показує найсильнішого.</summary>
        [Fact]
        public void Recalculate_ShouldLetPowerOutweighTheOtherAxes()
        {
            var fighter = NewRating();
            fighter.Recalculate(power: 100, buildingLevelSum: 0, Config(), Now);

            var builder = NewRating();
            builder.RecordActivity(quests: 5);
            builder.Recalculate(power: 0, buildingLevelSum: 100, Config(), Now);

            Assert.True(fighter.TotalRating > builder.TotalRating,
                $"Сила має домінувати: {fighter.TotalRating} проти {builder.TotalRating}.");
        }

        /// <summary>
        /// Поразка одразу опускає рейтинг: компоненти перераховуються
        /// з поточного стану, а не накопичуються.
        /// </summary>
        [Fact]
        public void Recalculate_ShouldDropWhenPowerFalls()
        {
            var rating = NewRating();

            rating.Recalculate(power: 100, buildingLevelSum: 0, Config(), Now);
            var before = rating.TotalRating;

            rating.Recalculate(power: 10, buildingLevelSum: 0, Config(), Now.AddHours(1));

            Assert.True(rating.TotalRating < before);
        }

        /// <summary>Кожен вид активності важить за своїм коефіцієнтом.</summary>
        [Fact]
        public void Recalculate_ShouldWeighActivityKindsSeparately()
        {
            var hunter = NewRating();
            hunter.RecordActivity(monsters: 4); // 4 × 5 = 20

            var questor = NewRating();
            questor.RecordActivity(quests: 4); // 4 × 20 = 80

            var config = Config();
            hunter.Recalculate(0, 0, config, Now);
            questor.Recalculate(0, 0, config, Now);

            Assert.True(questor.ActivityScore > hunter.ActivityScore);
        }

        /// <summary>Лічильники накопичуються, а не заміщаються.</summary>
        [Fact]
        public void RecordActivity_ShouldAccumulate()
        {
            var rating = NewRating();

            rating.RecordActivity(monsters: 3);
            rating.RecordActivity(monsters: 2, battlesWon: 1);

            Assert.Equal(5, rating.MonstersDefeated);
            Assert.Equal(1, rating.BattlesWon);
        }

        /// <summary>
        /// Активність не скидається перерахунком: він читає лічильники,
        /// а не заміщає їх.
        /// </summary>
        [Fact]
        public void Recalculate_ShouldNotResetCounters()
        {
            var rating = NewRating();
            rating.RecordActivity(monsters: 7, battlesWon: 3, quests: 2, contribution: 500);

            rating.Recalculate(power: 50, buildingLevelSum: 50, Config(), Now);

            Assert.Equal(7, rating.MonstersDefeated);
            Assert.Equal(3, rating.BattlesWon);
            Assert.Equal(2, rating.QuestsCompleted);
            Assert.Equal(500, rating.ServerContribution);
        }

        /// <summary>
        /// Нульовий орієнтир не валить перерахунок діленням на нуль:
        /// конфіг редагується руками, і виняток тут був би несподіванкою.
        /// </summary>
        [Fact]
        public void Recalculate_ShouldTolerateAZeroReference()
        {
            var config = Config();
            config.PowerReference = 0;

            var rating = NewRating();
            rating.Recalculate(power: 1000, buildingLevelSum: 0, config, Now);

            Assert.Equal(0, rating.PowerScore);
        }

        /// <summary>Перерахунок оновлює позначку часу — за нею розв'язується нічия в топі.</summary>
        [Fact]
        public void Recalculate_ShouldStampTheUpdateTime()
        {
            var rating = NewRating();
            var later = Now.AddHours(3);

            rating.Recalculate(power: 10, buildingLevelSum: 10, Config(), later);

            Assert.Equal(later, rating.UpdatedAt);
        }
    }
}
