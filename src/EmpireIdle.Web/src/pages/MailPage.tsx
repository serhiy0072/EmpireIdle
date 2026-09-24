import { useQueryClient } from "@tanstack/react-query";
import ErrorBanner from "../components/ErrorBanner";
import { useSession } from "../hooks/useSession";
import { useResolveRequest } from "../lib/queries/clans";
import {
  useMailbox,
  useMarkAnnouncementRead,
  useMarkLetterRead,
  type AnnouncementView,
  type MailLetterView,
} from "../lib/queries/mail";

const ANNOUNCEMENT_LABELS: Record<string, string> = {
  News: "Новина",
  Event: "Івент",
  Maintenance: "Технічні роботи",
};

const INVITE_STATES: Record<string, string> = {
  Accepted: "Прийнято",
  Declined: "Відхилено",
  Cancelled: "Клан скасував запрошення",
  Expired: "Строк минув",
  Gone: "Клан розпущено",
};

const date = (iso: string) =>
  new Date(iso).toLocaleString("uk-UA", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

const unreadDot = <span className="mr-1 inline-block h-2 w-2 rounded-full bg-emerald-500" aria-label="непрочитане" />;

interface LetterProps {
  playerId: string;
  letter: MailLetterView;
  onOpen: () => void;
}

/**
 * Лист-запрошення за теперішнім станом запрошення: кнопки лише в того,
 * що ще чекає. Після дії лист лишається — як історія (GDD §7.4).
 */
function InviteLetter({ playerId, letter, onOpen }: LetterProps) {
  const queryClient = useQueryClient();
  const resolve = useResolveRequest(playerId);
  const invite = letter.clanInvite;

  const answer = (approve: boolean) =>
    invite != null &&
    resolve.mutate(
      { requestId: invite.requestId, approve },
      { onSettled: () => void queryClient.invalidateQueries({ queryKey: ["mail", playerId] }) },
    );

  return (
    <li
      className="space-y-2 rounded-xl border border-slate-200 bg-white p-3"
      onMouseEnter={() => !letter.isRead && onOpen()}
      onClick={() => !letter.isRead && onOpen()}
    >
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span className="font-medium text-slate-800">
          {!letter.isRead && unreadDot}
          Запрошення в клан {invite != null && `${invite.clanName} [${invite.clanTag}]`}
        </span>
        <span className="text-xs text-slate-400">{date(letter.createdAt)}</span>
      </div>

      <ErrorBanner error={resolve.error} />

      {invite == null ? (
        <p className="text-sm text-slate-500">Запрошення вже недоступне.</p>
      ) : invite.canRespond ? (
        <div className="flex gap-2">
          <button
            type="button"
            disabled={resolve.isPending}
            onClick={() => answer(true)}
            className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            Прийняти
          </button>
          <button
            type="button"
            disabled={resolve.isPending}
            onClick={() => answer(false)}
            className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            Відхилити
          </button>
        </div>
      ) : (
        <p className="text-sm text-slate-500">{INVITE_STATES[invite.state] ?? invite.state}</p>
      )}
    </li>
  );
}

function Announcement({ announcement, onOpen }: { announcement: AnnouncementView; onOpen: () => void }) {
  return (
    <li
      className="space-y-1 rounded-xl border border-slate-200 bg-white p-3"
      onMouseEnter={() => !announcement.isRead && onOpen()}
      onClick={() => !announcement.isRead && onOpen()}
    >
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span className="font-medium text-slate-800">
          {!announcement.isRead && unreadDot}
          {announcement.title}
        </span>
        <span className="text-xs text-slate-400">
          {ANNOUNCEMENT_LABELS[announcement.kind] ?? announcement.kind} · {date(announcement.publishedAt)}
        </span>
      </div>
      <p className="whitespace-pre-wrap text-sm text-slate-700">{announcement.body}</p>
    </li>
  );
}

/**
 * Скринька (GDD §7.4): особисті листи й оголошення світу. Прочитаним лист
 * стає, щойно гравець на нього навів чи торкнувся, — без окремої кнопки.
 */
export default function MailPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const mailbox = useMailbox(playerId);
  const readLetter = useMarkLetterRead(playerId);
  const readAnnouncement = useMarkAnnouncementRead(playerId);

  if (mailbox.isPending) {
    return <p className="text-slate-500">Завантаження скриньки…</p>;
  }

  if (mailbox.isError) {
    return <ErrorBanner error={mailbox.error} />;
  }

  const { letters, announcements } = mailbox.data;

  return (
    <div className="space-y-5">
      <h1 className="text-xl font-medium text-slate-800">Скринька</h1>

      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Оголошення</h2>
        {announcements.length === 0 ? (
          <p className="text-sm text-slate-500">Оголошень немає.</p>
        ) : (
          <ul className="space-y-2">
            {announcements.map((announcement) => (
              <Announcement
                key={announcement.id}
                announcement={announcement}
                onOpen={() => readAnnouncement.mutate(announcement.id)}
              />
            ))}
          </ul>
        )}
      </section>

      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Листи</h2>
        {letters.length === 0 ? (
          <p className="text-sm text-slate-500">Листів немає.</p>
        ) : (
          <ul className="space-y-2">
            {letters.map((letter) => (
              <InviteLetter
                key={letter.id}
                playerId={playerId}
                letter={letter}
                onOpen={() => readLetter.mutate(letter.id)}
              />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
