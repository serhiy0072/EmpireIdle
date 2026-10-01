using System.Reflection;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Скидання щоденного квесту зіставляє нові Required із цілями за Index. EF без OrderBy
/// порядку колекції не гарантує, тож позиція в списку переплутала б значення між цілями.
/// </summary>
public class QuestProgressResetTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Reset_ShouldMatchObjectivesByIndex_EvenWhenLoadedOutOfOrder()
    {
        var progress = new QuestProgress(Guid.NewGuid(), Guid.NewGuid(), 1, "daily_mix", [5, 10], Now);

        // Так колекцію могла б віддати база: рядок першої цілі переїхав у heap за другою
        var objectives = (List<QuestObjectiveProgress>)typeof(QuestProgress)
            .GetField("_objectives", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(progress)!;
        objectives.Reverse();

        progress.Reset([7, 20], Now.AddDays(1));

        Assert.Equal(7, progress.Objectives.Single(o => o.Index == 0).Required);
        Assert.Equal(20, progress.Objectives.Single(o => o.Index == 1).Required);
    }
}
