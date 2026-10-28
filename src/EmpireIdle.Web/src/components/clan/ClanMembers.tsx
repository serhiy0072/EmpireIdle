import { useState } from "react";
import { Link } from "react-router-dom";
import type { ClanMemberResponse, ClanRoleResponse } from "../../lib/apiTypes";
import { clanRoleName } from "../../lib/clanRoles";
import { t } from "i18next";

interface Props {
  members: ClanMemberResponse[];
  roles: ClanRoleResponse[];
  myPlayerId: string;
  myRank: number;
  now: number;
  canKick: boolean;
  canAssignRoles: boolean;
  isLeader: boolean;
  busy: boolean;
  onKick: (playerId: string) => void;
  onAssignRole: (playerId: string, roleId: string) => void;
  onTransferLeadership: (playerId: string) => void;
}

function compact(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return Math.round(value).toString();
}

function since(iso: string, now: number): string {
  const days = Math.floor((now - Date.parse(iso)) / 86_400_000);

  return days <= 0 ? t("social:members.today") : t("social:members.daysAgo", { count: days });
}

/**
 * Список учасників за рангом ролі. Керувати можна лише тими, хто нижчий
 * за мене: такі ж правила на сервері, тож кнопки для рівних і вищих ховаємо.
 */
export default function ClanMembers({
  members,
  roles,
  myPlayerId,
  myRank,
  now,
  canKick,
  canAssignRoles,
  isLeader,
  busy,
  onKick,
  onAssignRole,
  onTransferLeadership,
}: Props) {
  // Передача лідерства незворотна для того, хто натиснув, — тож у два кліки
  const [confirmLeader, setConfirmLeader] = useState<string | null>(null);

  // Більший ранг — старший: лідер угорі
  const sorted = [...members].sort((a, b) => b.rank - a.rank || b.power - a.power);
  // Роль можна дати лише нижчу за свою
  const assignable = roles.filter((role) => role.rank < myRank);

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      <table className="w-full text-sm">
        <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
          <tr>
            <th className="px-3 py-2">{t("social:members.player")}</th>
            <th className="px-3 py-2">{t("social:members.role")}</th>
            <th className="px-3 py-2 text-right">{t("social:members.power")}</th>
            <th className="px-3 py-2">{t("social:members.activity")}</th>
            <th className="px-3 py-2" />
          </tr>
        </thead>
        <tbody>
          {sorted.map((member) => {
            const me = member.playerId === myPlayerId;
            const below = member.rank < myRank;

            return (
              <tr key={member.playerId} className={`border-t border-slate-100 ${me ? "bg-emerald-50/50" : ""}`}>
                <td className="px-3 py-2 font-medium text-slate-800">
                  {member.playerName}
                  {me ? (
                    <span className="ml-1 text-xs text-emerald-700">{t("social:members.you")}</span>
                  ) : (
                    <Link
                      to={`/chat?${new URLSearchParams({ tab: "Private", to: member.playerId, name: member.playerName }).toString()}`}
                      className="ml-2 text-xs font-normal text-emerald-700 hover:underline"
                    >
                      {t("social:members.message")}
                    </Link>
                  )}
                </td>
                <td className="px-3 py-2 text-slate-600">
                  {canAssignRoles && below && assignable.length > 0 ? (
                    <select
                      value={member.roleId}
                      disabled={busy}
                      onChange={(event) => onAssignRole(member.playerId, event.target.value)}
                      className="rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm"
                    >
                      {/* Поточна роль може бути вищою за доступні — лишаємо її в списку, щоб select не збрехав */}
                      {!assignable.some((role) => role.id === member.roleId) && (
                        <option value={member.roleId}>{clanRoleName(member.roleKey, member.roleName)}</option>
                      )}
                      {assignable.map((role) => (
                        <option key={role.id} value={role.id}>
                          {clanRoleName(role.key, role.name)}
                        </option>
                      ))}
                    </select>
                  ) : (
                    clanRoleName(member.roleKey, member.roleName)
                  )}
                </td>
                <td className="px-3 py-2 text-right text-slate-700">{compact(member.power)}</td>
                <td className="px-3 py-2 text-slate-500">{since(member.lastActiveAt, now)}</td>
                <td className="space-x-1 whitespace-nowrap px-3 py-2 text-right">
                  {isLeader && !me && (
                    <button
                      type="button"
                      onClick={() => {
                        if (confirmLeader === member.playerId) {
                          setConfirmLeader(null);
                          onTransferLeadership(member.playerId);
                        } else {
                          setConfirmLeader(member.playerId);
                        }
                      }}
                      onBlur={() => setConfirmLeader(null)}
                      disabled={busy}
                      className="rounded-lg border border-amber-300 px-2 py-0.5 text-xs text-amber-800 hover:bg-amber-50 disabled:opacity-50"
                    >
                      {confirmLeader === member.playerId ? t("social:members.confirmLeader") : t("social:members.makeLeader")}
                    </button>
                  )}
                  {canKick && below && (
                    <button
                      type="button"
                      onClick={() => onKick(member.playerId)}
                      disabled={busy}
                      className="rounded-lg border border-red-200 px-2 py-0.5 text-xs text-red-700 hover:bg-red-50 disabled:opacity-50"
                    >
                      {t("social:members.kick")}
                    </button>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
