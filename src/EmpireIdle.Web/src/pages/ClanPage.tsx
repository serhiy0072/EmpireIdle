import { useState } from "react";
import ClanBrowser from "../components/clan/ClanBrowser";
import ClanHelpPanel from "../components/clan/ClanHelpPanel";
import ClanMembers from "../components/clan/ClanMembers";
import ClanRequestsPanel from "../components/clan/ClanRequestsPanel";
import ClanSettings from "../components/clan/ClanSettings";
import ErrorBanner from "../components/ErrorBanner";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import {
  permissionLabel,
  useAssignRole,
  useClanApplications,
  useClanHelp,
  useClanInvites,
  useCreateClan,
  useGiveHelp,
  useInvitePlayer,
  useJoinClan,
  useKickMember,
  useLeaveClan,
  useMyClan,
  useRecallReinforcements,
  useRequestHelp,
  useResolveRequest,
  useUpdateClanSettings,
} from "../lib/queries/clans";
import { useGarrison } from "../lib/queries/garrison";
import { useVillage } from "../lib/queries/village";

type Tab = "help" | "members" | "requests" | "settings";

export default function ClanPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();

  const clan = useMyClan(playerId);
  const inClan = clan.data !== null && clan.data !== undefined;
  const permissions = new Set(clan.data?.myPermissions ?? []);
  const canRecruit = permissions.has("Recruit");

  const invites = useClanInvites(playerId);
  const help = useClanHelp(playerId, inClan);
  const applications = useClanApplications(playerId, inClan && canRecruit);
  const village = useVillage(playerId);
  const garrison = useGarrison(playerId);

  const create = useCreateClan(playerId);
  const join = useJoinClan(playerId);
  const leave = useLeaveClan(playerId);
  const kick = useKickMember(playerId);
  const assignRole = useAssignRole(playerId);
  const settings = useUpdateClanSettings(playerId);
  const requestHelp = useRequestHelp(playerId);
  const giveHelp = useGiveHelp(playerId);
  const recall = useRecallReinforcements(playerId);
  const invite = useInvitePlayer(playerId);
  const resolve = useResolveRequest(playerId);

  const [tab, setTab] = useState<Tab>("help");
  const [copied, setCopied] = useState(false);

  if (clan.isPending) {
    return <p className="text-slate-500">Завантаження клану…</p>;
  }

  if (clan.isError) {
    return <ErrorBanner error={clan.error} />;
  }

  const mutations = [create, join, leave, kick, assignRole, settings, requestHelp, giveHelp, recall, invite, resolve];
  const busy = mutations.some((mutation) => mutation.isPending);
  // Помилка лише останньої дії: інакше стара відмова однієї кнопки
  // (скажімо, створення клану) перекривала б свіжу відмову іншої
  const latest = mutations.reduce((last, mutation) => (mutation.submittedAt > last.submittedAt ? mutation : last));
  const failure = latest.error;

  const copyId = () => {
    void navigator.clipboard.writeText(playerId).then(() => {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1_500);
    });
  };

  if (clan.data === null) {
    return (
      <div className="space-y-4">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h1 className="text-xl font-medium text-slate-800">Клан</h1>
          <button type="button" onClick={copyId} className="font-mono text-xs text-slate-500 hover:text-slate-800">
            {copied ? "скопійовано" : `мій ID: ${playerId}`}
          </button>
        </div>
        <ErrorBanner error={failure} />
        <ClanBrowser
          invites={invites.data ?? []}
          now={now}
          busy={busy}
          joinOutcome={typeof join.data === "string" ? join.data : null}
          onJoin={(clanId) => join.mutate(clanId)}
          onCreate={(name, tag) => create.mutate({ name, tag })}
          onResolveInvite={(requestId, approve) => resolve.mutate({ requestId, approve })}
        />
      </div>
    );
  }

  const me = clan.data.members.find((member) => member.playerId === playerId);
  const myRank = me?.rank ?? 0;
  const isLeader = clan.data.roles.find((role) => role.id === clan.data?.myRoleId)?.isLeaderRole ?? false;

  const constructions = (village.data?.buildings ?? []).filter((building) => building.isUnderConstruction);
  const trainings = garrison.data?.trainingOrders ?? [];
  const helpItems = help.data ?? [];
  const pendingHelp = helpItems.filter((item) => !item.isMine && !item.alreadyHelped).length;

  const tabs: { key: Tab; label: string; visible: boolean }[] = [
    { key: "help", label: pendingHelp > 0 ? `Допомога · ${pendingHelp}` : "Допомога", visible: true },
    { key: "members", label: `Учасники · ${clan.data.memberCount}/${clan.data.capacity}`, visible: true },
    {
      key: "requests",
      label: (applications.data?.length ?? 0) > 0 ? `Набір · ${applications.data?.length}` : "Набір",
      visible: canRecruit,
    },
    { key: "settings", label: "Налаштування", visible: true },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">
          [{clan.data.tag}] {clan.data.name}
        </h1>
        <p className="text-sm text-slate-500">
          {me?.roleName ?? "—"}
          {permissions.size > 0 && ` · ${[...permissions].map(permissionLabel).join(", ")}`}
        </p>
      </div>

      <ErrorBanner error={failure} />

      <nav className="flex gap-1">
        {tabs
          .filter((item) => item.visible)
          .map((item) => (
            <button
              key={item.key}
              type="button"
              onClick={() => setTab(item.key)}
              className={`rounded-lg px-3 py-1 text-sm ${
                tab === item.key ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"
              }`}
            >
              {item.label}
            </button>
          ))}
      </nav>

      {tab === "help" && (
        <ClanHelpPanel
          items={helpItems}
          constructions={constructions}
          trainings={trainings}
          now={now}
          busy={busy}
          onGive={(requestId) => giveHelp.mutate(requestId)}
          onRequest={(targetType, targetId) => requestHelp.mutate({ targetType, targetId })}
        />
      )}

      {tab === "members" && (
        <ClanMembers
          members={clan.data.members}
          roles={clan.data.roles}
          myPlayerId={playerId}
          myRank={myRank}
          now={now}
          canKick={permissions.has("Kick")}
          canAssignRoles={permissions.has("AssignRoles")}
          busy={busy}
          onKick={(targetPlayerId) => kick.mutate(targetPlayerId)}
          onAssignRole={(targetPlayerId, roleId) => assignRole.mutate({ targetPlayerId, roleId })}
        />
      )}

      {tab === "requests" && canRecruit && (
        <ClanRequestsPanel
          applications={applications.data ?? []}
          now={now}
          busy={busy}
          onResolve={(requestId, approve) => resolve.mutate({ requestId, approve })}
          onInvite={(targetPlayerId) => invite.mutate(targetPlayerId)}
        />
      )}

      {tab === "settings" && (
        <div className="grid gap-4 md:grid-cols-2">
          <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
            <h3 className="font-medium text-slate-800">Профіль</h3>
            <ClanSettings
              clan={clan.data}
              canEdit={permissions.has("EditProfile")}
              busy={busy}
              onSave={(description, joinPolicy) => settings.mutate({ description, joinPolicy })}
            />
          </section>

          <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
            <h3 className="font-medium text-slate-800">Моє</h3>
            <button type="button" onClick={copyId} className="font-mono text-xs text-slate-500 hover:text-slate-800">
              {copied ? "скопійовано" : `мій ID: ${playerId}`}
            </button>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => recall.mutate()}
                disabled={busy}
                className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                Відкликати підкріплення
              </button>
              {/* Лідер не виходить, поки клан не передано: сервер відмовить, тож і кнопки нема */}
              {!isLeader && (
                <button
                  type="button"
                  onClick={() => leave.mutate()}
                  disabled={busy}
                  className="rounded-lg border border-red-200 px-3 py-1 text-sm text-red-700 hover:bg-red-50 disabled:opacity-50"
                >
                  Покинути клан
                </button>
              )}
            </div>
            {typeof recall.data === "number" && (
              <p className="text-xs text-slate-500">Відкликано маршів: {recall.data}</p>
            )}
          </section>
        </div>
      )}
    </div>
  );
}
