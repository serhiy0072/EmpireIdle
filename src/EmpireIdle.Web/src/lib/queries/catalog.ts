import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { api } from "../api";
import { queryKeys } from "../queryKeys";
import type { components } from "../schema";
import { useSession } from "../../hooks/useSession";
import { DEFAULT_LANGUAGE, usePlayerSettings } from "./player";

export type CatalogResponse = components["schemas"]["CatalogResponse"];
export type CatalogHero = components["schemas"]["CatalogHero"];
export type CatalogItem = components["schemas"]["CatalogItem"];
export type CatalogResource = components["schemas"]["CatalogResource"];
export type CatalogBuilding = components["schemas"]["CatalogBuilding"];
export type CatalogPassive = components["schemas"]["CatalogPassive"];
export type CatalogUnit = components["schemas"]["CatalogUnit"];
export type CatalogArtifactSet = components["schemas"]["CatalogArtifactSet"];
export type CatalogSetRarity = components["schemas"]["CatalogSetRarity"];
export type CatalogArtifactSlot = components["schemas"]["CatalogArtifactSlot"];

export interface Catalog {
  /** false, поки довідник не приїхав: екрани показують ключі замість назв. */
  loaded: boolean;
  hero: (key: string) => CatalogHero | null;
  /** Увесь ростер гри — для кодексу. */
  allHeroes: CatalogHero[];
  item: (key: string) => CatalogItem | null;
  building: (key: string) => CatalogBuilding | null;
  unit: (key: string) => CatalogUnit | null;
  heroName: (key: string) => string;
  itemName: (key: string) => string;
  resourceName: (key: string) => string;
  buildingName: (key: string) => string;
  unitName: (key: string) => string;
  /** Юніти, які ця будівля вже може тренувати на своєму поточному рівні. */
  unitsFor: (buildingType: string, buildingLevel: number) => CatalogUnit[];
  /** Ключі будівель, що тренують хоч один тип юнітів. */
  trainingBuildingKeys: string[];
  maxConstellation: number;
  maxTier: number;
  /** Кап рівня юніта від тренування чи прокачки (§5.2 GDD). */
  maxUnitLevel: number;
  /** Ціна лікування пораненого юніта в gems — та сама для будь-якого типу. */
  healGemsPerUnit: number;
  /** Зброя з ціною в золоті — те, що продає кузня. */
  weaponsForSale: CatalogItem[];
  /** Артефактні слоти героя за типом (намисто, корона…) у порядку номерів. */
  artifactSlots: CatalogArtifactSlot[];
  /** Назва слота, у який вдягається артефакт; null — не артефакт. */
  artifactSlotName: (itemKey: string) => string | null;
  /** Стеля заточки й прокачки спорядження. */
  maxEnhancement: number;
  /** Ремонт зброї в gems: база плюс надбавка за рівень заточки. */
  repairGemsBase: number;
  repairGemsPerLevel: number;
  /** Сторона світової мапи в клітинах. */
  mapSize: number;
  /** Ключ головної будівлі — її рівень показуємо як рівень гравця. */
  mainBuildingKey: string;
  /** Родини наборів артефактів у порядку рівнів. */
  artifactSets: CatalogArtifactSet[];
  /** Родина, до якої належить предмет; null — предмет поза наборами. */
  setOfItem: (itemKey: string) => CatalogArtifactSet | null;
  /**
   * Ціна прискорення таймера на момент `now` — та сама формула, що в SpeedUpCalculator:
   * платиться все понад межу (floorSeconds), щонайменше 1 gem. 0 — лишилась межа,
   * прискорювати нічого, кнопку не показуємо. Поки каталог не приїхав — ціна із запиту.
   */
  speedUpCost: (completesAt: string, now: number, serverCost: number) => number;
}

/**
 * Довідник гри. Тягнеться один раз: у межах запуску сервера він незмінний,
 * а після перезавантаження сторінки браузер отримає 304 за ETag.
 */
export function useCatalog(): Catalog {
  const session = useSession();
  const settings = usePlayerSettings(session?.playerId ?? "");
  const language = settings.data?.language ?? DEFAULT_LANGUAGE;

  const query = useQuery({
    queryKey: queryKeys.catalog(language),
    queryFn: () => api<CatalogResponse>(`/api/catalog?lang=${encodeURIComponent(language)}`),
    // Відкрита вкладка раз на 5 хвилин перевіряє версію — з ETag це 304 без тіла
    staleTime: 5 * 60_000,
  });

  return useMemo(() => {
    const data = query.data;

    const heroes = new Map((data?.heroes ?? []).map((hero) => [hero.key, hero]));
    const items = new Map((data?.items ?? []).map((item) => [item.key, item]));
    const resources = new Map((data?.resources ?? []).map((resource) => [resource.key, resource]));
    const buildings = new Map((data?.buildings ?? []).map((building) => [building.key, building]));
    const units = new Map((data?.units ?? []).map((unit) => [unit.key, unit]));
    // SetKey предмета («dawn_rare») → родина («dawn»): так картка артефакту знає свій набір
    const familyBySetKey = new Map(
      (data?.artifactSets ?? []).flatMap((set) => set.rarities.map((rarity) => [rarity.setKey, set] as const)),
    );

    return {
      loaded: data !== undefined,
      hero: (key) => heroes.get(key) ?? null,
      allHeroes: data?.heroes ?? [],
      item: (key) => items.get(key) ?? null,
      building: (key) => buildings.get(key) ?? null,
      unit: (key) => units.get(key) ?? null,
      // Ключ як запасна назва: краще технічний рядок, ніж порожнє місце
      heroName: (key) => heroes.get(key)?.displayName ?? key,
      itemName: (key) => items.get(key)?.displayName ?? key,
      resourceName: (key) => resources.get(key)?.displayName ?? key,
      buildingName: (key) => buildings.get(key)?.displayName ?? key,
      unitName: (key) => units.get(key)?.displayName ?? key,
      unitsFor: (buildingType, buildingLevel) =>
        (data?.units ?? []).filter(
          (unit) => unit.requiresBuilding === buildingType && unit.requiresBuildingLevel <= buildingLevel,
        ),
      trainingBuildingKeys: [
        ...new Set((data?.units ?? []).flatMap((unit) => (unit.requiresBuilding == null ? [] : [unit.requiresBuilding]))),
      ],
      maxConstellation: data?.maxConstellation ?? 6,
      maxTier: data?.maxTier ?? 3,
      maxUnitLevel: data?.maxUnitLevel ?? 10,
      healGemsPerUnit: data?.healGemsPerUnit ?? 1,
      weaponsForSale: (data?.items ?? []).filter((item) => item.slot === "Weapon" && item.priceGold > 0),
      artifactSlots: data?.artifactSlots ?? [],
      artifactSlotName: (itemKey) => {
        const slot = items.get(itemKey)?.artifactSlot;
        return slot == null ? null : (data?.artifactSlots.find((s) => s.key === slot)?.displayName ?? slot);
      },
      maxEnhancement: data?.maxEnhancement ?? 20,
      repairGemsBase: data?.repairGemsBase ?? 20,
      repairGemsPerLevel: data?.repairGemsPerLevel ?? 8,
      mapSize: data?.mapSize ?? 500,
      mainBuildingKey: data?.mainBuildingKey ?? "townhall",
      artifactSets: data?.artifactSets ?? [],
      setOfItem: (itemKey) => {
        const setKey = items.get(itemKey)?.setKey;
        return setKey == null ? null : (familyBySetKey.get(setKey) ?? null);
      },
      speedUpCost: (completesAt, now, serverCost) => {
        if (data === undefined) return serverCost;

        const { floorSeconds, factor, exponent } = data.speedUp;
        const minutes = (new Date(completesAt).getTime() - now - floorSeconds * 1_000) / 60_000;

        return minutes <= 0 ? 0 : Math.max(1, Math.ceil(factor * Math.pow(minutes, exponent)));
      },
    };
  }, [query.data]);
}

const RANK_STYLES: Record<string, string> = {
  Common: "bg-slate-100 text-slate-600",
  Rare: "bg-sky-100 text-sky-800",
  Unique: "bg-amber-100 text-amber-800",
};

const RANK_LABELS: Record<string, string> = {
  Common: "Звичайний",
  Rare: "Рідкісний",
  Unique: "Унікальний",
};

export function rankStyle(rank: string | undefined): string {
  return RANK_STYLES[rank ?? ""] ?? "bg-slate-100 text-slate-600";
}

export function rankLabel(rank: string | undefined): string {
  return RANK_LABELS[rank ?? ""] ?? "—";
}

const STATES: Record<string, string> = {
  Idle: "Вдома",
  Deployed: "У поході",
  Wounded: "Поранений",
  OnMarket: "На ринку",
  LevelingUp: "Качається",
};

export function heroState(state: string): string {
  return STATES[state] ?? state;
}

/** Поточна сила пасивки: база плюс приріст за кожне сузір'я понад те, що її відкрило. */
export function passivePercent(passive: CatalogPassive, constellation: number): number | null {
  if (constellation < passive.unlockConstellation) return null;

  return passive.basePercent + passive.percentPerConstellation * (constellation - passive.unlockConstellation);
}