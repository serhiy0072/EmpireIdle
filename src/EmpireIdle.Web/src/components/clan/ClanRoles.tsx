import { useState } from "react";
import type { ClanPermission, ClanRoleRequest, ClanRoleResponse, MyClanResponse } from "../../lib/apiTypes";
import { clanRoleName } from "../../lib/clanRoles";
import { CLAN_PERMISSIONS, permissionLabel } from "../../lib/queries/clans";
import { t } from "i18next";

interface Props {
  clan: MyClanResponse;
  /** Є дозвіл ManageRoles; без нього — лише перелік ролей. */
  canManage: boolean;
  busy: boolean;
  onCreate: (role: ClanRoleRequest) => void;
  onUpdate: (roleId: string, role: ClanRoleRequest) => void;
  onDelete: (roleId: string) => void;
}

/** Довжина назви ролі на сервері (ClanRoles.Name). */
const MAX_NAME = 32;

const button = "rounded-lg border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 disabled:opacity-50";

/**
 * Ролі клану (GDD §7.1): перелік із рангами й дозволами та редактор для тих, хто має ManageRoles.
 * Сервер дозволяє керувати лише ролями нижчого рангу й видавати лише власні дозволи — форма показує
 * ті самі межі, щоб відмова не була несподіванкою.
 */
export default function ClanRoles({ clan, canManage, busy, onCreate, onUpdate, onDelete }: Props) {
  // "new" — форма нової ролі; id — редагування наявної; null — лише перелік
  const [editing, setEditing] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);

  const mine = clan.roles.find((role) => role.id === clan.myRoleId);
  const myRank = mine?.rank ?? 0;
  const roles = [...clan.roles].sort((a, b) => b.rank - a.rank);
  const manageable = (role: ClanRoleResponse) => canManage && !role.isLeaderRole && role.rank < myRank;

  return (
    <div className="space-y-2">
      <ul className="divide-y divide-slate-100">
        {roles.map((role) => (
          <li key={role.id} className="space-y-2 py-2 text-sm">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium text-slate-800">{clanRoleName(role.key, role.name)}</span>
              <span className="text-xs text-slate-500">{t("social:roles.rank", { rank: role.rank })}</span>
              <span className="flex-1 text-xs text-slate-500">
                {role.permissions.length === 0 ? t("social:roles.noPermissions") : role.permissions.map(permissionLabel).join(", ")}
              </span>
              {manageable(role) && (
                <>
                  <button type="button" onClick={() => setEditing(editing === role.id ? null : role.id)} disabled={busy} className={button}>
                    {editing === role.id ? t("social:roles.cancel") : t("social:roles.edit")}
                  </button>
                  {!role.isDefaultRole && (
                    <button
                      type="button"
                      onClick={() => {
                        // Видалення не скасувати — два кліки, як і виключення учасника
                        if (confirmDelete === role.id) {
                          setConfirmDelete(null);
                          onDelete(role.id);
                        } else {
                          setConfirmDelete(role.id);
                        }
                      }}
                      onBlur={() => setConfirmDelete(null)}
                      disabled={busy}
                      className="rounded-lg border border-red-200 px-2 py-0.5 text-xs text-red-700 hover:bg-red-50 disabled:opacity-50"
                    >
                      {confirmDelete === role.id ? t("social:roles.confirmDelete") : t("social:roles.delete")}
                    </button>
                  )}
                </>
              )}
            </div>

            {editing === role.id && (
              <RoleForm
                // Вбудована роль показує переклад, але зберігається назва: порожнє поле не дало б її лишити
                initial={{ name: clanRoleName(role.key, role.name), rank: role.rank, permissions: role.permissions }}
                myRank={myRank}
                myPermissions={clan.myPermissions}
                busy={busy}
                onSubmit={(next) => {
                  onUpdate(role.id, next);
                  setEditing(null);
                }}
              />
            )}
          </li>
        ))}
      </ul>

      {!canManage ? null : editing === "new" ? (
        <RoleForm
          initial={{ name: "", rank: Math.max(0, myRank - 1), permissions: [] }}
          myRank={myRank}
          myPermissions={clan.myPermissions}
          busy={busy}
          onSubmit={(next) => {
            onCreate(next);
            setEditing(null);
          }}
        />
      ) : (
        <button type="button" onClick={() => setEditing("new")} disabled={busy || myRank === 0} className={button}>
          {t("social:roles.add")}
        </button>
      )}
    </div>
  );
}

interface FormProps {
  initial: ClanRoleRequest;
  myRank: number;
  myPermissions: ClanPermission[];
  busy: boolean;
  onSubmit: (role: ClanRoleRequest) => void;
}

function RoleForm({ initial, myRank, myPermissions, busy, onSubmit }: FormProps) {
  const [name, setName] = useState(initial.name);
  const [rank, setRank] = useState(initial.rank);
  const [permissions, setPermissions] = useState<ClanPermission[]>(initial.permissions);

  const toggle = (permission: ClanPermission) =>
    setPermissions((current) => (current.includes(permission) ? current.filter((item) => item !== permission) : [...current, permission]));

  const valid = name.trim() !== "" && Number.isInteger(rank) && rank >= 0 && rank < myRank;

  return (
    <div className="space-y-2 rounded-lg bg-slate-50 p-3">
      <div className="flex flex-wrap gap-3">
        <label className="block flex-1 text-xs text-slate-600">
          {t("social:roles.name")}
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={MAX_NAME}
            className="mt-1 w-full rounded-lg border border-slate-300 px-2 py-1 text-sm text-slate-800"
          />
        </label>
        <label className="block w-28 text-xs text-slate-600">
          {t("social:roles.rankField")}
          <input
            type="number"
            min={0}
            max={myRank - 1}
            value={rank}
            onChange={(event) => setRank(Number(event.target.value))}
            className="mt-1 w-full rounded-lg border border-slate-300 px-2 py-1 text-sm text-slate-800"
          />
        </label>
      </div>
      <p className="text-xs text-slate-500">{t("social:roles.rankHint", { max: myRank - 1 })}</p>

      <div className="flex flex-wrap gap-x-4 gap-y-1">
        {CLAN_PERMISSIONS.map((permission) => (
          <label key={permission} className="flex items-center gap-1 text-xs text-slate-700">
            <input
              type="checkbox"
              checked={permissions.includes(permission)}
              // Видати можна лише те, що маєш сам; уже виданий чужий дозвіл дозволяємо хіба зняти
              disabled={!myPermissions.includes(permission) && !permissions.includes(permission)}
              onChange={() => toggle(permission)}
            />
            {permissionLabel(permission)}
          </label>
        ))}
      </div>

      <button
        type="button"
        onClick={() => onSubmit({ name: name.trim(), rank, permissions })}
        disabled={busy || !valid}
        className="rounded-lg bg-slate-800 px-3 py-1 text-sm font-medium text-white hover:bg-slate-900 disabled:opacity-50"
      >
        {t("social:roles.save")}
      </button>
    </div>
  );
}
