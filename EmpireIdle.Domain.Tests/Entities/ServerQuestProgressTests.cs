using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Tests.Entities
{
    public class ServerQuestProgressTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static ServerQuestProgress NewProgress(long target = 100)
            => new(Guid.NewGuid(), 1, "server_cleanup", target);

        /// <summary>Нижче цілі — квест триває, сигналу про завершення немає.</summary>
        [Fact]
        public void UpdateTotal_ShouldStayInProgress_BelowTheTarget()
        {
            var progress = NewProgress();

            var completed = progress.UpdateTotal(99, Now);

            Assert.False(completed);
            Assert.Equal(QuestState.InProgress, progress.State);
            Assert.Equal(99, progress.Total);
        }

        /// <summary>Досягнення цілі повертає true — саме за цим джоб знає, що пора роздавати.</summary>
        [Fact]
        public void UpdateTotal_ShouldSignalCompletion_AtTheTarget()
        {
            var progress = NewProgress();

            Assert.True(progress.UpdateTotal(100, Now));
            Assert.Equal(QuestState.Completed, progress.State);
            Assert.Equal(Now, progress.CompletedAt);
        }

        /// <summary>
        /// Повторне досягнення сигналу не дає. Без цього кожен прогін джоба
        /// після завершення запускав би роздачу нагород наново.
        /// </summary>
        [Fact]
        public void UpdateTotal_ShouldSignalOnlyOnce()
        {
            var progress = NewProgress();
            progress.UpdateTotal(100, Now);

            Assert.False(progress.UpdateTotal(150, Now.AddMinutes(1)));
        }

        /// <summary>Час завершення фіксується на першому перетині, не на останньому оновленні.</summary>
        [Fact]
        public void UpdateTotal_ShouldKeepTheFirstCompletionTime()
        {
            var progress = NewProgress();
            progress.UpdateTotal(100, Now);

            progress.UpdateTotal(500, Now.AddHours(5));

            Assert.Equal(Now, progress.CompletedAt);
        }

        /// <summary>Перевищення цілі теж завершує: сума з внесків рідко влучає рівно.</summary>
        [Fact]
        public void UpdateTotal_ShouldCompleteWhenOvershooting()
        {
            var progress = NewProgress();

            Assert.True(progress.UpdateTotal(1000, Now));
        }
    }

    public class ServerQuestContributionTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static ServerQuestContribution NewContribution()
            => new(Guid.NewGuid(), 1, "server_cleanup", Guid.NewGuid());

        /// <summary>Внески накопичуються, а не заміщаються.</summary>
        [Fact]
        public void Add_ShouldAccumulate()
        {
            var contribution = NewContribution();

            contribution.Add(30, Now);
            contribution.Add(20, Now.AddMinutes(5));

            Assert.Equal(50, contribution.Amount);
        }

        /// <summary>
        /// Час останнього внеску рухається — за ним розв'язується нічия в ранзі:
        /// хто набрав ту саму суму раніше, той вище.
        /// </summary>
        [Fact]
        public void Add_ShouldMoveTheLastContributionTime()
        {
            var contribution = NewContribution();
            var later = Now.AddHours(2);

            contribution.Add(10, Now);
            contribution.Add(10, later);

            Assert.Equal(later, contribution.LastContributedAt);
        }

        /// <summary>Позначка видачі одноразова — вона й є захистом від подвійної роздачі.</summary>
        [Fact]
        public void MarkRewarded_ShouldSucceedOnlyOnce()
        {
            var contribution = NewContribution();

            Assert.True(contribution.MarkRewarded(Now));
            Assert.False(contribution.MarkRewarded(Now.AddHours(1)));
            Assert.Equal(Now, contribution.RewardedAt);
        }
    }
}
