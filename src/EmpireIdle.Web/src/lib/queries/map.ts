import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { MapAreaResponse, MapCellDetailsResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";

/** Скільки клітин навколо центру тягнемо за раз: 25×25 — один запит, без прокрутки в межах вікна. */
export const MAP_RADIUS = 12;

/**
 * Ділянка мапи. Довго не стаpіє: місцевість незмінна, а окупантів оновлює
 * подія бою чи ручний рух центру. enabled — центр невідомий, поки не приїхало село.
 */
export function useMapArea(centerX: number | null, centerY: number | null): UseQueryResult<MapAreaResponse> {
  return useQuery({
    queryKey: queryKeys.mapArea(centerX ?? 0, centerY ?? 0, MAP_RADIUS),
    queryFn: () => api<MapAreaResponse>(`/api/map?centerX=${centerX}&centerY=${centerY}&radius=${MAP_RADIUS}`),
    enabled: centerX !== null && centerY !== null,
    staleTime: 60_000,
  });
}

/** Деталі клітини: для монстра — рівень і склад загону, щоб напад був вибором, а не лотереєю. */
export function useMapCell(x: number | null, y: number | null): UseQueryResult<MapCellDetailsResponse> {
  return useQuery({
    queryKey: queryKeys.mapCell(x ?? 0, y ?? 0),
    queryFn: () => api<MapCellDetailsResponse>(`/api/map/cell/${x}/${y}`),
    enabled: x !== null && y !== null,
    staleTime: 30_000,
  });
}
