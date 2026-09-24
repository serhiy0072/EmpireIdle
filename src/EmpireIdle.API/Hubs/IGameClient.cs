using EmpireIdle.API.Hubs.Events;

namespace EmpireIdle.API.Hubs
{
    /// <summary>
    /// Події, які сервер шле гравцю. Ім'я методу — ім'я події на дроті,
    /// тож перейменування тут ламає компіляцію, а не мовчки клієнта.
    ///
    /// Без CancellationToken у сигнатурах: типізований проксі SignalR будує
    /// реалізацію рефлексією й приймає лише методи з payload-параметрами.
    /// Відправка все одно fire-and-forget — скасовувати нічого.
    /// </summary>
    public interface IGameClient
    {
        Task BuildingCollected(BuildingCollectedEvent payload);

        Task UpgradeStarted(UpgradeStartedEvent payload);

        Task UpgradeCompleted(UpgradeCompletedEvent payload);

        Task BattleFinished(BattleFinishedEvent payload);

        Task MarchReturned(MarchReturnedEvent payload);

        Task ServerQuestRewarded(ServerQuestRewardedEvent payload);

        Task ClanInvite(ClanInviteEvent payload);

        Task ChatMessage(ChatMessageEvent payload);
    }
}
