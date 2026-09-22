import { useState } from "react";
import type { ClanJoinPolicy, MyClanResponse } from "../../lib/apiTypes";
import { JOIN_POLICIES, joinPolicyLabel } from "../../lib/queries/clans";

interface Props {
  clan: MyClanResponse;
  canEdit: boolean;
  busy: boolean;
  onSave: (description: string, joinPolicy: ClanJoinPolicy) => void;
}

/** Опис і політика вступу. Без права EditProfile — лише перегляд. */
export default function ClanSettings({ clan, canEdit, busy, onSave }: Props) {
  const [description, setDescription] = useState(clan.description);
  const [policy, setPolicy] = useState<ClanJoinPolicy>(
    JOIN_POLICIES.find((item) => item.name === clan.joinPolicy)?.value ?? 0,
  );

  if (!canEdit) {
    return (
      <div className="space-y-1 text-sm">
        <p className="text-slate-700">{clan.description === "" ? "Опису немає." : clan.description}</p>
        <p className="text-slate-500">Вступ: {joinPolicyLabel(clan.joinPolicy)}</p>
      </div>
    );
  }

  const dirty = description !== clan.description || JOIN_POLICIES[policy]?.name !== clan.joinPolicy;

  return (
    <div className="space-y-3">
      <label className="block text-sm text-slate-600">
        Опис
        <textarea
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          maxLength={500}
          rows={3}
          className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-800"
        />
      </label>

      <label className="block text-sm text-slate-600">
        Вступ
        <select
          value={policy}
          onChange={(event) => setPolicy(Number(event.target.value) as ClanJoinPolicy)}
          className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-800"
        >
          {JOIN_POLICIES.map((item) => (
            <option key={item.value} value={item.value}>
              {joinPolicyLabel(item.name)}
            </option>
          ))}
        </select>
      </label>

      <button
        type="button"
        onClick={() => onSave(description.trim(), policy)}
        disabled={busy || !dirty}
        className="rounded-lg bg-slate-800 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-900 disabled:opacity-50"
      >
        Зберегти
      </button>
    </div>
  );
}
