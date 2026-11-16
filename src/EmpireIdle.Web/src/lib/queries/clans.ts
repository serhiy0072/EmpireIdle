import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type {
  ClanApplicationResponse,
  ClanHelpItemResponse,
  ClanHelpTarget,
  ClanInviteResponse,
  ClanJoinPolicy,
  ClanListResponse,
  ClanPermission,
  ClanProfileResponse,
  ClanRoleRequest,
  MyClanResponse,
} from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";
import { refetchAtDue } from "./polling";
import { t } from "i18next";

/**
 * Поза кланом сервер відповідає 204 без тіла — це стан, а не помилка.
 * React Query не приймає undefined як дані, тож порожню відповідь зводимо до null.
 */
export function useMyClan(playerId: string): UseQueryResult<MyClanResponse | null> {
  return useQuery({
    queryKey: queryKeys.clan(playerId),
    queryFn: async () => (await api<MyClanResponse | undefined>(`/api/clans/${playerId}`)) ?? null,
  });
}

export function useClanHelp(playerId: string, enabled: boolean): UseQueryResult<ClanHelpItemResponse[]> {
  return useQuery({
    queryKey: queryKeys.clanHelp(playerId),
    queryFn: () => api<ClanHelpItemResponse[]>(`/api/clans/${playerId}/help`),
    enabled,
    // Прохання гаснуть за терміном без події
    refetchInterval: refetchAtDue<ClanHelpItemResponse[]>((items) => items.map((item) => item.expiresAt)),
  });
}

/** Заявки бачить лише той, хто має право приймати: без нього запит не робимо. */
export function useClanApplications(playerId: string, enabled: boolean): UseQueryResult<ClanApplicationResponse[]> {
  return useQuery({
    queryKey: queryKeys.clanApplications(playerId),
    queryFn: () => api<ClanApplicationResponse[]>(`/api/clans/${playerId}/requests`),
    enabled,
  });
}

export function useClanInvites(playerId: string): UseQueryResult<ClanInviteResponse[]> {
  return useQuery({
    queryKey: queryKeys.clanInvites(playerId),
    queryFn: () => api<ClanInviteResponse[]>(`/api/clans/${playerId}/requests/invites`),
  });
}

export function useClanBrowse(search: string, page: number): UseQueryResult<ClanListResponse> {
  const query = new URLSearchParams({ page: page.toString(), pageSize: "20" });

  if (search !== "") query.set("search", search);

  return useQuery({
    queryKey: queryKeys.clanBrowse(search, page),
    queryFn: () => api<ClanListResponse>(`/api/clans?${query.toString()}`),
    // Нова сторінка приходить без миготіння порожнім списком
    placeholderData: (previous) => previous,
  });
}

export function useClanProfile(clanId: string | null): UseQueryResult<ClanProfileResponse> {
  return useQuery({
    queryKey: queryKeys.clanProfile(clanId ?? ""),
    queryFn: () => api<ClanProfileResponse>(`/api/clans/profile/${clanId}`),
    enabled: clanId !== null,
  });
}

/** Усі кланові команди без тіла: усе потрібне серверу — в маршруті. */
function useClanCommand<TInput>(
  playerId: string,
  request: (input: TInput) => { path: string; method?: "POST" | "PUT" | "DELETE"; body?: unknown },
  scopes: readonly PlayerScope[],
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: TInput) => {
      const { path, method = "POST", body } = request(input);
      return api<unknown>(path, { method, body, idempotent: true });
    },
    onSuccess: () => {
      invalidatePlayer(queryClient, playerId, scopes);
      // Каталог кланів і профілі — спільні: після вступу чи виходу лічильники учасників інші
      void queryClient.invalidateQueries({ queryKey: ["clans"] });
    },
  });
}

/**
 * Усе, що змінює сам факт членства — створення, вступ, прийняте запрошення: клан відкриває бонус території,
 * кланові квести, дерево технологій і полиці кланового магазину.
 */
const CLAN_MEMBERSHIP_SCOPES: readonly PlayerScope[] = ["clan", "clanRequests", "territory", "quests", "clanTechnologies", "clanShop"];

export function useCreateClan(playerId: string) {
  return useClanCommand<{ name: string; tag: string }>(
    playerId,
    (input) => ({ path: `/api/clans/${playerId}`, body: input }),
    CLAN_MEMBERSHIP_SCOPES,
  );
}

/** Відкритий клан приймає одразу, за схваленням — створює заявку; сервер повертає результат рядком. */
export function useJoinClan(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (clanId) => ({ path: `/api/clans/${playerId}/join/${clanId}` }),
    CLAN_MEMBERSHIP_SCOPES,
  );
}

/** Вихід повертає війська в обидва боки: свої з чужих сіл і споруд, чужі — зі свого. */
export function useLeaveClan(playerId: string) {
  return useClanCommand<void>(playerId, () => ({ path: `/api/clans/${playerId}/leave` }), [
    "clan",
    "clanHelp",
    "marches",
    "garrison",
    "heroes",
    "territory",
    "clanTechnologies",
    "power",
  ]);
}

/** Кік повертає війська виключеного з нашого гарнізону — і наш гарнізон, і марші змінюються. */
export function useKickMember(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (targetPlayerId) => ({ path: `/api/clans/${playerId}/kick/${targetPlayerId}` }),
    ["clan", "garrison", "marches"],
  );
}

/** Лідер передає лідерство учаснику й сам стає на другу за рангом роль. */
export function useTransferLeadership(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (targetPlayerId) => ({ path: `/api/clans/${playerId}/members/${targetPlayerId}/leadership` }),
    ["clan"],
  );
}

export function useAssignRole(playerId: string) {
  return useClanCommand<{ targetPlayerId: string; roleId: string }>(
    playerId,
    (input) => ({ path: `/api/clans/${playerId}/members/${input.targetPlayerId}/role`, body: { roleId: input.roleId } }),
    ["clan"],
  );
}

/** Власні ролі клану: створення, зміна й видалення. Видалена роль переводить своїх носіїв у звичайні учасники. */
export function useCreateClanRole(playerId: string) {
  return useClanCommand<ClanRoleRequest>(playerId, (role) => ({ path: `/api/clans/${playerId}/roles`, body: role }), ["clan"]);
}

export function useUpdateClanRole(playerId: string) {
  return useClanCommand<{ roleId: string; role: ClanRoleRequest }>(
    playerId,
    (input) => ({ path: `/api/clans/${playerId}/roles/${input.roleId}`, method: "PUT", body: input.role }),
    ["clan"],
  );
}

export function useDeleteClanRole(playerId: string) {
  return useClanCommand<string>(playerId, (roleId) => ({ path: `/api/clans/${playerId}/roles/${roleId}`, method: "DELETE" }), ["clan"]);
}

export function useUpdateClanSettings(playerId: string) {
  return useClanCommand<{ description: string; joinPolicy: ClanJoinPolicy }>(
    playerId,
    (input) => ({ path: `/api/clans/${playerId}/settings`, method: "PUT", body: input }),
    ["clan"],
  );
}

export function useRequestHelp(playerId: string) {
  return useClanCommand<{ targetType: ClanHelpTarget; targetId: string }>(
    playerId,
    (input) => ({ path: `/api/clans/${playerId}/help`, body: input }),
    ["clanHelp"],
  );
}

/** Допомога скорочує чужий таймер, але моє село й гарнізон теж могли бути ціллю. */
export function useGiveHelp(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (requestId) => ({ path: `/api/clans/${playerId}/help/${requestId}` }),
    ["clanHelp", "village", "garrison"],
  );
}

export function useRecallReinforcements(playerId: string) {
  return useClanCommand<void>(
    playerId,
    () => ({ path: `/api/clans/${playerId}/reinforcements/recall` }),
    ["marches", "garrison"],
  );
}

export function useInvitePlayer(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (targetPlayerId) => ({ path: `/api/clans/${playerId}/requests/invite/${targetPlayerId}` }),
    ["clanRequests"],
  );
}

/** Одна команда на заявки до клану й на запрошення мені: сервер знає, чий це запит. */
export function useResolveRequest(playerId: string) {
  return useClanCommand<{ requestId: string; approve: boolean }>(
    playerId,
    (input) => ({ path: `/api/clans/${playerId}/requests/${input.requestId}/resolve`, body: { approve: input.approve } }),
    [...CLAN_MEMBERSHIP_SCOPES, "clanHelp"],
  );
}

export function useCancelRequest(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (requestId) => ({ path: `/api/clans/${playerId}/requests/${requestId}/cancel` }),
    ["clanRequests"],
  );
}

const POLICIES = ["Open", "ByApproval", "InviteOnly"] as const;

export function joinPolicyLabel(policy: string): string {
  return (POLICIES as readonly string[]).includes(policy) ? t(`social:clan.policy.${policy as (typeof POLICIES)[number]}`) : policy;
}

/** Значення ClanJoinPolicy з контракту — у порядку, в якому їх показує вибір. */
export const JOIN_POLICIES: readonly ClanJoinPolicy[] = ["Open", "ByApproval", "InviteOnly"];

/** Дозволи клану в порядку, в якому їх показує редактор ролей; satisfies ловить новий дозвіл без підпису. */
export const CLAN_PERMISSIONS = [
  "Recruit",
  "Kick",
  "AssignRoles",
  "ManageRoles",
  "EditProfile",
  "BuildStructures",
  "ManageTechnologies",
  "Disband",
] as const satisfies readonly Exclude<ClanPermission, "None">[];

export function permissionLabel(permission: string): string {
  return (CLAN_PERMISSIONS as readonly string[]).includes(permission)
    ? t(`social:clan.permission.${permission as (typeof CLAN_PERMISSIONS)[number]}`)
    : permission;
}
