import { useState } from "react";
import type { ClanApplicationResponse } from "../../lib/apiTypes";
import { formatRemaining } from "../../lib/time";
import { formatNumber } from "../../lib/format";
import { t } from "i18next";

interface Props {
  applications: ClanApplicationResponse[];
  now: number;
  busy: boolean;
  onResolve: (requestId: string, approve: boolean) => void;
  onInvite: (targetPlayerId: string) => void;
}

const button = "rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50";

/** Набір: заявки на вступ і запрошення за ідентифікатором гравця. Видно лише з правом Recruit. */
export default function ClanRequestsPanel({ applications, now, busy, onResolve, onInvite }: Props) {
  const [target, setTarget] = useState("");
  const validTarget = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(target.trim());

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <section className="space-y-2">
        <h3 className="text-sm font-medium uppercase tracking-wide text-slate-500">{t("social:requests.title", { count: applications.length })}</h3>
        {applications.length === 0 ? (
          <p className="text-sm text-slate-500">{t("social:requests.none")}</p>
        ) : (
          applications.map((application) => (
            <div key={application.requestId} className="flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 bg-white p-3">
              <div className="flex-1 text-sm">
                <span className="font-medium text-slate-800">{application.playerName}</span>
                <span className="ml-2 text-xs text-slate-500">
                  {t("social:requests.info", { power: formatNumber(Math.round(application.power)), remaining: formatRemaining(application.expiresAt, now) })}
                </span>
              </div>
              <button
                type="button"
                onClick={() => onResolve(application.requestId, true)}
                disabled={busy}
                className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
              >
                {t("social:requests.accept")}
              </button>
              <button type="button" onClick={() => onResolve(application.requestId, false)} disabled={busy} className={button}>
                {t("social:requests.decline")}
              </button>
            </div>
          ))
        )}
      </section>

      <section className="space-y-2">
        <h3 className="text-sm font-medium uppercase tracking-wide text-slate-500">{t("social:requests.invite")}</h3>
        <div className="flex gap-2">
          <input
            value={target}
            onChange={(event) => setTarget(event.target.value)}
            placeholder={t("social:requests.playerId")}
            className="flex-1 rounded-lg border border-slate-300 px-3 py-1.5 font-mono text-sm text-slate-800"
          />
          <button
            type="button"
            onClick={() => {
              onInvite(target.trim());
              setTarget("");
            }}
            disabled={busy || !validTarget}
            className={button}
          >
            {t("social:requests.send")}
          </button>
        </div>
        <p className="text-xs text-slate-500">{t("social:requests.idHint")}</p>
      </section>
    </div>
  );
}
