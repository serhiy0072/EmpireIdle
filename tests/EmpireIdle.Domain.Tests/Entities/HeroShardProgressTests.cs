using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>
    /// Уламки — це золото гравця, перетворене на прогрес. Помилка тут
    /// або з'їдає купівлю, або віддає героя дешевше, ніж мало б.
    /// </summary>
    public class HeroShardProgressTests
    {
        private const int ServerId = 1;

        private static HeroShardProgress CreateProgress()
            => new(Guid.NewGuid(), Guid.NewGuid(), ServerId, "warrior_bran");

        [Fact]
        public void NewProgress_ShouldStartEmpty()
            => Assert.Equal(0, CreateProgress().Count);

        [Fact]
        public void Add_ShouldAccumulateAcrossPurchases()
        {
            var progress = CreateProgress();

            progress.Add(4);
            progress.Add(6);

            Assert.Equal(10, progress.Count);
        }

        /// <summary>
        /// Нуль і від'ємне — це не «нічого не сталося», а зіпсований запит:
        /// мовчазне ігнорування приховало б помилку в хендлері.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Add_ShouldRejectNonPositiveCount(int count)
            => Assert.Throws<ArgumentOutOfRangeException>(() => CreateProgress().Add(count));

        /// <summary>Набравши поріг, гравець витрачає рівно його, не все підряд.</summary>
        [Fact]
        public void TryConsume_ShouldSpendExactlyTheRequiredAmount()
        {
            var progress = CreateProgress();
            progress.Add(13);

            Assert.True(progress.TryConsume(10));
            Assert.Equal(3, progress.Count);
        }

        /// <summary>
        /// Нижче порогу — відмова без списання. Часткове зняття
        /// з'їло б уже куплені уламки.
        /// </summary>
        [Fact]
        public void TryConsume_ShouldKeepEverything_WhenBelowTheThreshold()
        {
            var progress = CreateProgress();
            progress.Add(9);

            Assert.False(progress.TryConsume(10));
            Assert.Equal(9, progress.Count);
        }

        /// <summary>Рівно поріг — призов проходить, залишок нуль.</summary>
        [Fact]
        public void TryConsume_ShouldSucceed_AtExactlyTheThreshold()
        {
            var progress = CreateProgress();
            progress.Add(10);

            Assert.True(progress.TryConsume(10));
            Assert.Equal(0, progress.Count);
        }

        /// <summary>
        /// Після призову рядок живе далі: гравець продовжує збирати
        /// на сузір'я того самого героя.
        /// </summary>
        [Fact]
        public void TryConsume_ShouldAllowASecondSummonAfterRefilling()
        {
            var progress = CreateProgress();
            progress.Add(10);
            progress.TryConsume(10);

            progress.Add(10);

            Assert.True(progress.TryConsume(10));
        }

        /// <summary>Порожній прогрес не витрачається.</summary>
        [Fact]
        public void TryConsume_ShouldRefuse_WhenNothingCollected()
            => Assert.False(CreateProgress().TryConsume(1));
    }
}
