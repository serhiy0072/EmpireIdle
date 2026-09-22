import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { MapAreaResponse, MapCellDetailsResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";

/** Межі радіуса ділянки: нижня — щоб не смикати сервер на кожен крок, верхня — стеля API (51×51). */
export const MAP_RADIUS_MIN = 8;
export const MAP_RADIUS_MAX = 25;

/** Що показує камера: центр і скільки клітин довкола вміщає кадр. */
export interface MapView {
  x: number;
  y: number;
  radius: number;
}

/**
 * Ділянка мапи. Довго не стаpіє: місцевість незмінна, а окупантів оновлює
 * подія бою чи рух камери. enabled — центр невідомий, поки не приїхало село.
 */
export function useMapArea(view: MapView | null): UseQueryResult<MapAreaResponse> {
  return useQuery({
    queryKey: queryKeys.mapArea(view?.x ?? 0, view?.y ?? 0, view?.radius ?? 0),
    queryFn: () => api<MapAreaResponse>(`/api/map?centerX=${view?.x}&centerY=${view?.y}&radius=${view?.radius}`),
    enabled: view !== null,
    staleTime: 60_000,
    // Камера поїхала далі — стара ділянка лишається на екрані, поки не приїде нова
    placeholderData: (previous) => previous,
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
