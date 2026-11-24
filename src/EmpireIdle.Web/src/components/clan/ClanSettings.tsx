import { useState } from "react";
import type { ClanJoinPolicy, MyClanResponse } from "../../lib/apiTypes";
import { JOIN_POLICIES, joinPolicyLabel } from "../../lib/queries/clans";
import { t } from "i18next";

interface Props {
  clan: MyClanResponse;
  canEdit: boolean;
  busy: boolean;
  onSave: (description: string, joinPolicy: ClanJoinPolicy) => void;
}

/** Опис і політика вступу. Без права EditProfile — лише перегляд. */
export default function ClanSettings({ clan, canEdit, busy, onSave }: Props) {
  const [description, setDescription] = useState(clan.description);
  const [policy, setPolicy] = useState<ClanJoinPolicy>(JOIN_POLICIES.find((item) => item === clan.joinPolicy) ?? "Open");

  if (!canEdit) {
    return (
      <div className="space-y-1 text-sm">
        <p className="text-slate-100">{clan.description === "" ? t("social:settings.noDescription") : clan.description}</p>
        <p className="text-sky-100/70">{t("social:settings.joinPolicy", { policy: joinPolicyLabel(clan.joinPolicy) })}</p>
      </div>
    );
  }

  const dirty = description !== clan.description || policy !== clan.joinPolicy;

  return (
    <div className="space-y-3">
      <label className="block text-sm text-slate-200">
        {t("social:settings.description")}
        <textarea
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          maxLength={500}
          rows={3}
          className="mt-1 w-full rounded-lg border border-[#4a72b0] px-3 py-1.5 text-sm text-white"
        />
      </label>

      <label className="block text-sm text-slate-200">
        {t("social:settings.join")}
        <select
          value={policy}
          onChange={(event) => setPolicy(event.target.value as ClanJoinPolicy)}
          className="mt-1 w-full rounded-lg border border-[#4a72b0] bg-[#1d3b6f] px-3 py-1.5 text-sm text-white"
        >
          {JOIN_POLICIES.map((item) => (
            <option key={item} value={item}>
              {joinPolicyLabel(item)}
            </option>
          ))}
        </select>
      </label>

      <button
        type="button"
        onClick={() => onSave(description.trim(), policy)}
        disabled={busy || !dirty}
        className="rounded-lg bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-500 disabled:opacity-50"
      >
        {t("social:settings.save")}
      </button>
    </div>
  );
}
