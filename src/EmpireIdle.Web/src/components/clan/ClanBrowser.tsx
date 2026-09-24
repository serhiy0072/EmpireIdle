import { useState } from "react";
import type { ClanInviteResponse } from "../../lib/apiTypes";
import { clanNameIsValid, clanNameRules, clanTagIsValid, clanTagRules, type ClanRule } from "../../lib/clanRules";
import { joinPolicyLabel, useClanBrowse } from "../../lib/queries/clans";
import { formatRemaining } from "../../lib/time";

interface Props {
  invites: ClanInviteResponse[];
  now: number;
  busy: boolean;
  /** Результат останнього вступу: "Joined" або "ApplicationSubmitted". */
  joinOutcome: string | null;
  onJoin: (clanId: string) => void;
  onCreate: (name: string, tag: string) => void;
  onResolveInvite: (requestId: string, approve: boolean) => void;
}

const input = "rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-800";

/** Невиконані правила — лише коли гравець уже щось увів: порожня форма не має кричати. */
function UnmetRules({ rules, value }: { rules: ClanRule[]; value: string }) {
  const unmet = value === "" ? [] : rules.filter((rule) => !rule.passed(value));

  if (unmet.length === 0) return null;

  return (
    <ul className="mt-1 space-y-0.5">
      {unmet.map((rule) => (
        <li key={rule.label} className="text-xs text-amber-700">
          • {rule.label}
        </li>
      ))}
    </ul>
  );
}
const button = "rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50";

/** Гравець поза кланом: запрошення, пошук по каталогу й створення свого. */
export default function ClanBrowser({ invites, now, busy, joinOutcome, onJoin, onCreate, onResolveInvite }: Props) {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [name, setName] = useState("");
  const [tag, setTag] = useState("");

  const clans = useClanBrowse(search.trim(), page);
  const pages = clans.data === undefined ? 1 : Math.max(1, Math.ceil(clans.data.total / clans.data.pageSize));

  return (
    <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
      <div className="space-y-4">
        {joinOutcome === "ApplicationSubmitted" && (
          <div role="status" className="rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
            Заявку подано — чекайте на рішення клану.
          </div>
        )}

        {invites.length > 0 && (
          <section className="space-y-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Запрошення</h2>
            {invites.map((invite) => (
              <div key={invite.requestId} className="flex flex-wrap items-center gap-2 rounded-xl border border-violet-200 bg-white p-3">
                <div className="flex-1">
                  <span className="font-medium text-slate-800">
                    [{invite.clanTag}] {invite.clanName}
                  </span>
                  <span className="ml-2 text-xs text-slate-500">
                    {invite.memberCount}/{invite.capacity} · діє {formatRemaining(invite.expiresAt, now)}
                  </span>
                  {invite.description !== "" && <p className="text-sm text-slate-600">{invite.description}</p>}
                </div>
                <button
                  type="button"
                  onClick={() => onResolveInvite(invite.requestId, true)}
                  disabled={busy}
                  className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                >
                  Прийняти
                </button>
                <button type="button" onClick={() => onResolveInvite(invite.requestId, false)} disabled={busy} className={button}>
                  Відхилити
                </button>
              </div>
            ))}
          </section>
        )}

        <section className="space-y-2">
          <div className="flex items-center gap-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Клани</h2>
            <input
              type="search"
              placeholder="Назва або тег"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
              className={`ml-auto ${input}`}
            />
          </div>

          {clans.isPending ? (
            <p className="text-sm text-slate-500">Шукаємо…</p>
          ) : clans.isError ? (
            <p className="text-sm text-red-700">Не вдалося завантажити каталог.</p>
          ) : clans.data.items.length === 0 ? (
            <p className="text-sm text-slate-500">Нічого не знайдено.</p>
          ) : (
            <ul className="space-y-2">
              {clans.data.items.map((clan) => (
                <li key={clan.id} className="flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 bg-white p-3">
                  <div className="flex-1">
                    <span className="font-medium text-slate-800">
                      [{clan.tag}] {clan.name}
                    </span>
                    <span className="ml-2 text-xs text-slate-500">
                      {clan.memberCount}/{clan.capacity} · {joinPolicyLabel(clan.joinPolicy)}
                    </span>
                    {clan.description !== "" && <p className="text-sm text-slate-600">{clan.description}</p>}
                  </div>
                  {/* Закритий клан приймає лише за запрошенням — кнопки нема, щоб не ловити відмову */}
                  {clan.joinPolicy !== "InviteOnly" && (
                    <button type="button" onClick={() => onJoin(clan.id)} disabled={busy || clan.isFull} className={button}>
                      {clan.isFull ? "Повний" : clan.joinPolicy === "Open" ? "Вступити" : "Подати заявку"}
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}

          {pages > 1 && (
            <div className="flex items-center justify-center gap-2 text-sm">
              <button type="button" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={page <= 1} className={button}>
                ←
              </button>
              <span className="text-slate-600">
                {page} / {pages}
              </span>
              <button type="button" onClick={() => setPage((value) => Math.min(pages, value + 1))} disabled={page >= pages} className={button}>
                →
              </button>
            </div>
          )}
        </section>
      </div>

      <section className="h-fit space-y-3 rounded-xl border border-slate-200 bg-white p-4">
        <h2 className="font-medium text-slate-800">Створити клан</h2>
        <label className="block text-sm text-slate-600">
          Назва
          <input value={name} onChange={(event) => setName(event.target.value)} maxLength={32} className={`mt-1 w-full ${input}`} />
          <UnmetRules rules={clanNameRules} value={name.trim()} />
        </label>
        <label className="block text-sm text-slate-600">
          Тег
          <input
            value={tag}
            onChange={(event) => setTag(event.target.value.toUpperCase())}
            maxLength={5}
            className={`mt-1 w-full font-mono uppercase ${input}`}
          />
          <UnmetRules rules={clanTagRules} value={tag.trim()} />
        </label>
        <button
          type="button"
          onClick={() => onCreate(name.trim(), tag.trim())}
          disabled={busy || !clanNameIsValid(name.trim()) || !clanTagIsValid(tag.trim())}
          className="w-full rounded-lg bg-slate-800 px-3 py-2 text-sm font-medium text-white hover:bg-slate-900 disabled:opacity-50"
        >
          Заснувати
        </button>
      </section>
    </div>
  );
}
