import { useMyClan, useClanHelp } from "../lib/queries/clans";
import { useKeyChests } from "../lib/queries/lootboxes";
import { useMailUnread } from "../lib/queries/mail";
import { QUEST_STATE, useQuests } from "../lib/queries/quests";

/**
 * Що чекає на гравця в кожному розділі: ключ — шлях розділу, значення — скільки
 * (число стає цифрою на значку, 0 — значка немає). Крапка гасне сама, щойно
 * нагороду забрано: запити інвалідуються тими ж мутаціями, що й сторінки.
 */
export function useAttention(playerId: string): Record<string, number> {
  const mail = useMailUnread(playerId);
  const quests = useQuests(playerId);
  const chests = useKeyChests(playerId);
  const clan = useMyClan(playerId);
  const help = useClanHelp(playerId, clan.data != null);

  return {
    "/mail": mail.data ?? 0,
    // Виконаний, але не забраний квест
    "/quests": (quests.data ?? []).filter((quest) => quest.state === QUEST_STATE.completed).length,
    // Безкоштовна скриня на сьогодні ще є
    "/shop": (chests.data ?? []).reduce((sum, chest) => sum + chest.freeLeftToday, 0),
    // Соклановці просять допомоги, а ти ще не допоміг
    "/clan": (help.data ?? []).filter(
      (item) => !item.isMine && !item.alreadyHelped && item.helpCount < item.maxHelpers,
    ).length,
  };
}
