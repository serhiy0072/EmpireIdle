import { useQueryClient } from "@tanstack/react-query";
import ErrorBanner from "../components/ErrorBanner";
import { useSession } from "../hooks/useSession";
import RewardLetter from "../components/mail/RewardLetter";
import ScoutReportLetter from "../components/mail/ScoutReportLetter";
import { useCatalog } from "../lib/queries/catalog";
import { useResolveRequest } from "../lib/queries/clans";
import {
  useClaimAll,
  useClaimLetter,
  useMailbox,
  useMarkAnnouncementRead,
  useMarkLetterRead,
  type AnnouncementView,
  type MailLetterView,
} from "../lib/queries/mail";
import { formatMoment } from "../lib/format";
import { t } from "i18next";

const ANNOUNCEMENTS = ["News", "Event", "Maintenance"] as const;

const INVITE_STATES = ["Accepted", "Declined", "Cancelled", "Expired", "Gone"] as const;

function announcementLabel(kind: string): string {
  return (ANNOUNCEMENTS as readonly string[]).includes(kind) ? t(`social:mail.announcement.${kind as (typeof ANNOUNCEMENTS)[number]}`) : kind;
}

function inviteStateLabel(state: string): string {
  return (INVITE_STATES as readonly string[]).includes(state) ? t(`social:mail.inviteState.${state as (typeof INVITE_STATES)[number]}`) : state;
}

const date = (iso: string) => formatMoment(iso);

const unreadDot = <span className="mr-1 inline-block h-2 w-2 rounded-full bg-emerald-500" aria-label={t("social:mail.unread")} />;

interface LetterProps {
  playerId: string;
  letter: MailLetterView;
  onOpen: () => void;
}

/** Лист про падіння міста: хто виселив, звідки й куди, до коли щит (GDD §2.6). */
function CityFallLetter({ letter, onOpen }: Omit<LetterProps, "playerId">) {
  const fall = letter.cityFall;

  return (
    <li
      className="space-y-1 rounded-xl border border-rose-200 bg-white p-3"
      onMouseEnter={() => !letter.isRead && onOpen()}
      onClick={() => !letter.isRead && onOpen()}
    >
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span className="font-medium text-slate-800">
          {!letter.isRead && unreadDot}
          {t("social:mail.fallTitle")}
        </span>
        <span className="text-xs text-slate-400">{date(letter.createdAt)}</span>
      </div>
      {fall == null ? (
        <p className="text-sm text-slate-500">{t("social:mail.noDetails")}</p>
      ) : (
        <p className="text-sm text-slate-700">
          {t("social:mail.fall", {
            attacker: fall.attackerName,
            fromX: fall.fromX,
            fromY: fall.fromY,
            toX: fall.toX,
            toY: fall.toY,
            until: date(fall.shieldUntil),
          })}
        </p>
      )}
    </li>
  );
}

/** Лист про зруйновану споруду клану: де стояла й хто зруйнував (GDD §7.2). */
function StructureFallLetter({ letter, onOpen }: Omit<LetterProps, "playerId">) {
  const fall = letter.structureFall;

  return (
    <li
      className="space-y-1 rounded-xl border border-rose-200 bg-white p-3"
      onMouseEnter={() => !letter.isRead && onOpen()}
      onClick={() => !letter.isRead && onOpen()}
    >
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span className="font-medium text-slate-800">
          {!letter.isRead && unreadDot}
          {t("social:mail.structureTitle")}
        </span>
        <span className="text-xs text-slate-400">{date(letter.createdAt)}</span>
      </div>
      {fall == null ? (
        <p className="text-sm text-slate-500">{t("social:mail.noDetails")}</p>
      ) : (
        <p className="text-sm text-slate-700">
          {t("social:mail.structure", { attacker: fall.attackerName, x: fall.x, y: fall.y })}
        </p>
      )}
    </li>
  );
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
          {t("social:mail.inviteTitle")} {invite != null && `${invite.clanName} [${invite.clanTag}]`}
        </span>
        <span className="text-xs text-slate-400">{date(letter.createdAt)}</span>
      </div>

      <ErrorBanner error={resolve.error} />

      {invite == null ? (
        <p className="text-sm text-slate-500">{t("social:mail.inviteGone")}</p>
      ) : invite.canRespond ? (
        <div className="flex gap-2">
          <button
            type="button"
            disabled={resolve.isPending}
            onClick={() => answer(true)}
            className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            {t("social:mail.accept")}
          </button>
          <button
            type="button"
            disabled={resolve.isPending}
            onClick={() => answer(false)}
            className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            {t("social:mail.decline")}
          </button>
        </div>
      ) : (
        <p className="text-sm text-slate-500">{inviteStateLabel(invite.state)}</p>
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
          {announcementLabel(announcement.kind)} · {date(announcement.publishedAt)}
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
  const claimLetter = useClaimLetter(playerId);
  const claimAll = useClaimAll(playerId);
  const catalog = useCatalog();

  if (mailbox.isPending) {
    return <p className="text-slate-500">{t("social:mail.loading")}</p>;
  }

  if (mailbox.isError) {
    return <ErrorBanner error={mailbox.error} />;
  }

  const { letters, announcements } = mailbox.data;
  const claimable = letters.filter((letter) => letter.canClaim).length;
  const claiming = claimLetter.isPending || claimAll.isPending;

  return (
    <div className="space-y-5">
      <div className="flex items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">{t("social:mail.title")}</h1>
        {claimable > 0 && (
          <button
            type="button"
            disabled={claiming}
            onClick={() => claimAll.mutate()}
            className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            {t("social:mail.claimAll", { count: claimable })}
          </button>
        )}
      </div>

      <ErrorBanner error={claimAll.error ?? claimLetter.error} />

      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">{t("social:mail.announcements")}</h2>
        {announcements.length === 0 ? (
          <p className="text-sm text-slate-500">{t("social:mail.noAnnouncements")}</p>
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
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">{t("social:mail.letters")}</h2>
        {letters.length === 0 ? (
          <p className="text-sm text-slate-500">{t("social:mail.noLetters")}</p>
        ) : (
          <ul className="space-y-2">
            {letters.map((letter) =>
              letter.rewards.length > 0 ? (
                <RewardLetter
                  key={letter.id}
                  letter={letter}
                  catalog={catalog}
                  busy={claiming}
                  onClaim={() => claimLetter.mutate(letter.id)}
                  onOpen={() => readLetter.mutate(letter.id)}
                />
              ) : letter.kind === "CityFall" ? (
                <CityFallLetter key={letter.id} letter={letter} onOpen={() => readLetter.mutate(letter.id)} />
              ) : letter.kind === "ScoutReport" ? (
                <ScoutReportLetter key={letter.id} letter={letter} onOpen={() => readLetter.mutate(letter.id)} />
              ) : letter.kind === "StructureFall" ? (
                <StructureFallLetter key={letter.id} letter={letter} onOpen={() => readLetter.mutate(letter.id)} />
              ) : (
                <InviteLetter
                  key={letter.id}
                  playerId={playerId}
                  letter={letter}
                  onOpen={() => readLetter.mutate(letter.id)}
                />
              ),
            )}
          </ul>
        )}
      </section>
    </div>
  );
}
