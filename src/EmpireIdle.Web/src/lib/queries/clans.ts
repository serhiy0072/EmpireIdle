import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type {
  ClanApplicationResponse,
  ClanHelpItemResponse,
  ClanHelpTarget,
  ClanInviteResponse,
  ClanJoinPolicy,
  ClanListResponse,
  ClanProfileResponse,
  MyClanResponse,
} from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer, type PlayerScope } from "./invalidate";
import { refetchAtDue } from "./polling";

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
  request: (input: TInput) => { path: string; method?: "POST" | "PUT"; body?: unknown },
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

export function useCreateClan(playerId: string) {
  return useClanCommand<{ name: string; tag: string }>(
    playerId,
    (input) => ({ path: `/api/clans/${playerId}`, body: input }),
    ["clan", "clanRequests"],
  );
}

/** Відкритий клан приймає одразу, за схваленням — створює заявку; сервер повертає результат рядком. */
export function useJoinClan(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (clanId) => ({ path: `/api/clans/${playerId}/join/${clanId}` }),
    ["clan", "clanRequests"],
  );
}

export function useLeaveClan(playerId: string) {
  return useClanCommand<void>(playerId, () => ({ path: `/api/clans/${playerId}/leave` }), ["clan", "clanHelp"]);
}

export function useKickMember(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (targetPlayerId) => ({ path: `/api/clans/${playerId}/kick/${targetPlayerId}` }),
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
    ["clan", "clanRequests", "clanHelp"],
  );
}

export function useCancelRequest(playerId: string) {
  return useClanCommand<string>(
    playerId,
    (requestId) => ({ path: `/api/clans/${playerId}/requests/${requestId}/cancel` }),
    ["clanRequests"],
  );
}

const POLICY_LABELS: Record<string, string> = {
  Open: "Відкритий",
  ByApproval: "За схваленням",
  InviteOnly: "Лише за запрошенням",
};

export function joinPolicyLabel(policy: string): string {
  return POLICY_LABELS[policy] ?? policy;
}

/** Числові значення ClanJoinPolicy з контракту — сервер приймає enum числом. */
export const JOIN_POLICIES: { value: ClanJoinPolicy; name: string }[] = [
  { value: 0, name: "Open" },
  { value: 1, name: "ByApproval" },
  { value: 2, name: "InviteOnly" },
];

const PERMISSION_LABELS: Record<string, string> = {
  Recruit: "набір",
  Kick: "виключення",
  AssignRoles: "ролі учасників",
  ManageRoles: "керування ролями",
  EditProfile: "профіль",
  BuildStructures: "споруди",
  Disband: "розпуск",
};

export function permissionLabel(permission: string): string {
  return PERMISSION_LABELS[permission] ?? permission;
}
