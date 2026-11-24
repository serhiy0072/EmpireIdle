import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import ClanBrowser from "../components/clan/ClanBrowser";
import ClanHelpPanel from "../components/clan/ClanHelpPanel";
import ClanMembers from "../components/clan/ClanMembers";
import ClanRequestsPanel from "../components/clan/ClanRequestsPanel";
import ClanRoles from "../components/clan/ClanRoles";
import ClanSettings from "../components/clan/ClanSettings";
import ClanShopPanel from "../components/clan/ClanShopPanel";
import ClanTechnologiesPanel from "../components/clan/ClanTechnologiesPanel";
import ClanTerritoryPanel from "../components/clan/ClanTerritoryPanel";
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
  useTransferLeadership,
  useCreateClanRole,
  useDeleteClanRole,
  useUpdateClanRole,
  useUpdateClanSettings,
} from "../lib/queries/clans";
import { clanRoleName } from "../lib/clanRoles";
import { useClanTechnologies } from "../lib/queries/clanTechnologies";
import { useGarrison } from "../lib/queries/garrison";
import { compactPower, usePower } from "../lib/queries/rating";
import { useClanTerritory } from "../lib/queries/territory";
import { useVillage } from "../lib/queries/village";
import { latestError } from "../lib/latestError";
import { GameScreen, GameTabs } from "../components/game/GameUi";
import { t } from "i18next";

type Tab = "help" | "members" | "territory" | "technologies" | "shop" | "requests" | "settings";

const TABS: readonly string[] = ["help", "members", "territory", "technologies", "shop", "requests", "settings"];

/** Вкладка — частина адреси (/clan/technologies): на неї веде посилання, а підказки впізнають екран. */
function tabOf(route: string | undefined): Tab {
  return route !== undefined && TABS.includes(route) ? (route as Tab) : "help";
}

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
  const territory = useClanTerritory(playerId, inClan);
  const technologies = useClanTechnologies(playerId, inClan);
  const power = usePower(playerId);

  const create = useCreateClan(playerId);
  const join = useJoinClan(playerId);
  const leave = useLeaveClan(playerId);
  const kick = useKickMember(playerId);
  const assignRole = useAssignRole(playerId);
  const transferLeadership = useTransferLeadership(playerId);
  const settings = useUpdateClanSettings(playerId);
  const createRole = useCreateClanRole(playerId);
  const updateRole = useUpdateClanRole(playerId);
  const deleteRole = useDeleteClanRole(playerId);
  const requestHelp = useRequestHelp(playerId);
  const giveHelp = useGiveHelp(playerId);
  const recall = useRecallReinforcements(playerId);
  const invite = useInvitePlayer(playerId);
  const resolve = useResolveRequest(playerId);

  const navigate = useNavigate();
  const tab = tabOf(useParams().tab);
  const setTab = (next: Tab) => void navigate(next === "help" ? "/clan" : `/clan/${next}`, { replace: true });
  const [copied, setCopied] = useState(false);
  // Вихід і тим паче розпуск не скасувати — другий клік, як у передачі лідерства
  const [confirmLeave, setConfirmLeave] = useState(false);

  if (clan.isPending) {
    return <p className="text-slate-500">{t("social:page.loading")}</p>;
  }

  if (clan.isError) {
    return <ErrorBanner error={clan.error} />;
  }

  const mutations = [
    create,
    join,
    leave,
    kick,
    assignRole,
    transferLeadership,
    settings,
    createRole,
    updateRole,
    deleteRole,
    requestHelp,
    giveHelp,
    recall,
    invite,
    resolve,
  ];
  const busy = mutations.some((mutation) => mutation.isPending);
  const failure = latestError(mutations);

  const copyId = () => {
    void navigator.clipboard.writeText(playerId).then(() => {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1_500);
    });
  };

  // Поза кланом магазин відкривається окремим екраном замість каталогу кланів
  if (clan.data === null && tab === "shop") {
    return (
      <GameScreen
        title={t("technologies:shop.title")}
        actions={
          <button type="button" onClick={() => setTab("help")} className="text-sm text-sky-200 hover:underline">
            {t("technologies:shop.back")}
          </button>
        }
      >
        <ClanShopPanel playerId={playerId} now={now} />
      </GameScreen>
    );
  }

  if (clan.data === null) {
    return (
      <GameScreen
        title={t("social:page.title")}
        actions={
          <div className="flex items-baseline gap-3">
            <button
              type="button"
              onClick={() => setTab("shop")}
              className="rounded-lg border border-amber-400/50 bg-amber-500/15 px-3 py-1 text-sm text-amber-200 hover:bg-amber-500/25"
            >
              🪙 {t("technologies:shop.title")}
            </button>
            <button type="button" onClick={copyId} className="font-mono text-xs text-sky-100/70 hover:text-white">
              {copied ? t("social:page.copied") : t("social:page.myId", { id: playerId })}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
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
      </GameScreen>
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
    { key: "help", label: pendingHelp > 0 ? t("social:page.tabHelpCount", { count: pendingHelp }) : t("social:page.tabHelp"), visible: true },
    { key: "members", label: t("social:page.tabMembers", { count: clan.data.memberCount, capacity: clan.data.capacity }), visible: true },
    { key: "territory", label: t("social:page.tabTerritory"), visible: true },
    { key: "technologies", label: t("social:page.tabTechnologies"), visible: true },
    { key: "shop", label: t("social:page.tabShop"), visible: true },
    {
      key: "requests",
      label: (applications.data?.length ?? 0) > 0 ? t("social:page.tabRecruitCount", { count: applications.data?.length }) : t("social:page.tabRecruit"),
      visible: canRecruit,
    },
    { key: "settings", label: t("social:page.tabSettings"), visible: true },
  ];

  return (
    <GameScreen
      title={`[${clan.data.tag}] ${clan.data.name}`}
      actions={
        <p className="text-sm text-sky-100/70">
          {me === undefined ? "—" : clanRoleName(me.roleKey, me.roleName)}
          {permissions.size > 0 && ` · ${[...permissions].map(permissionLabel).join(", ")}`}
        </p>
      }
    >
      <div className="space-y-4">
        <ErrorBanner error={failure} />

        {/* Вкладки й панель — одна картка, як на екранах героя й рюкзака */}
        <GameTabs
          tabs={tabs.filter((item) => item.visible).map((item) => ({ key: item.key, label: item.label }))}
          value={tab}
          onChange={setTab}
        />

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
            isLeader={isLeader}
            busy={busy}
            onKick={(targetPlayerId) => kick.mutate(targetPlayerId)}
            onAssignRole={(targetPlayerId, roleId) => assignRole.mutate({ targetPlayerId, roleId })}
            onTransferLeadership={(targetPlayerId) => transferLeadership.mutate(targetPlayerId)}
          />
        )}

        {tab === "territory" &&
          (territory.isError ? (
            <ErrorBanner error={territory.error} />
          ) : territory.data == null ? (
            <p className="text-sm text-sky-100/70">{t("social:page.loadingShort")}</p>
          ) : (
            <ClanTerritoryPanel territory={territory.data} myPlayerId={playerId} />
          ))}

        {tab === "technologies" &&
          (technologies.isError ? (
            <ErrorBanner error={technologies.error} />
          ) : technologies.data == null ? (
            <p className="text-sm text-sky-100/70">{t("social:page.loadingShort")}</p>
          ) : (
            <ClanTechnologiesPanel
              playerId={playerId}
              view={technologies.data}
              now={now}
              canMark={permissions.has("ManageTechnologies")}
            />
          ))}

        {tab === "shop" && <ClanShopPanel playerId={playerId} now={now} />}

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
            {/* Ролі бачать усі учасники; редактор усередині з'являється лише для ролей, якими можна керувати */}
            {clan.data !== null && (
              <section className="space-y-3 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-4 md:col-span-2">
                <h3 className="font-medium text-white">{t("social:roles.title")}</h3>
                <ClanRoles
                  clan={clan.data}
                  canManage={permissions.has("ManageRoles")}
                  busy={busy}
                  onCreate={(role, done) => createRole.mutate(role, { onSuccess: done })}
                  onUpdate={(roleId, role, done) => updateRole.mutate({ roleId, role }, { onSuccess: done })}
                  onDelete={(roleId) => deleteRole.mutate(roleId)}
                />
              </section>
            )}

            <section className="space-y-3 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-4">
              <h3 className="font-medium text-white">{t("social:page.profile")}</h3>
              <ClanSettings
                clan={clan.data}
                canEdit={permissions.has("EditProfile")}
                busy={busy}
                onSave={(description, joinPolicy) => settings.mutate({ description, joinPolicy })}
              />
            </section>

            <section className="space-y-3 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-4">
              <h3 className="font-medium text-white">{t("social:page.mine")}</h3>
              <button type="button" onClick={copyId} className="font-mono text-xs text-sky-100/70 hover:text-white">
                {copied ? t("social:page.copied") : t("social:page.myId", { id: playerId })}
              </button>
              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  onClick={() => recall.mutate()}
                  disabled={busy}
                  className="rounded-lg border border-[#4a72b0] px-3 py-1 text-sm text-slate-100 hover:bg-white/10 disabled:opacity-50"
                >
                  {t("social:page.recall")}
                </button>
                {/* Лідер теж може вийти: лідерство перейде найвищому за рангом, а сам-один — розпускає клан */}
                <button
                  type="button"
                  onClick={() => {
                    if (confirmLeave) {
                      setConfirmLeave(false);
                      leave.mutate();
                    } else {
                      setConfirmLeave(true);
                    }
                  }}
                  onBlur={() => setConfirmLeave(false)}
                  disabled={busy}
                  title={
                    isLeader && clan.data.memberCount > 1
                      ? t("social:page.leaderLeaves")
                      : undefined
                  }
                  className="rounded-lg border border-red-400/50 px-3 py-1 text-sm text-red-200 hover:bg-red-500/15 disabled:opacity-50"
                >
                  {confirmLeave
                    ? t("social:page.confirmLeave")
                    : isLeader && clan.data.memberCount <= 1
                      ? t("social:page.disband")
                      : t("social:page.leave")}
                </button>
              </div>
              {/* Бонуси кланових технологій діють лише в клані: вихід забирає і їх, і силу, яку вони дають */}
              {confirmLeave && (power.data?.clanTechnology ?? 0) > 0 && (
                <p className="rounded-lg bg-amber-500/15 px-3 py-2 text-xs text-amber-200">
                  {t("social:page.leaveLosesPower", { power: compactPower(power.data?.clanTechnology ?? 0) })}
                </p>
              )}
              {typeof recall.data === "number" && (
                <p className="text-xs text-sky-100/70">{t("social:page.recalled", { count: recall.data })}</p>
              )}
            </section>
          </div>
        )}
      </div>
    </GameScreen>
  );
}
