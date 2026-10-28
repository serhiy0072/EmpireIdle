import type { BeastResponse } from "../../lib/queries/beasts";
import { t } from "i18next";

const EFFECTS = ["Production", "Attack", "Defense", "MarchSpeed", "Carry"] as const;

function effectLabel(effect: string): string {
  return (EFFECTS as readonly string[]).includes(effect) ? t(`village:beasts.effect.${effect as (typeof EFFECTS)[number]}`) : effect;
}

export const percent = (value: number) => `${Math.round(value * 1000) / 10}%`;

/** Що дає пасивка звіра одним рядком: «+21% швидкість маршів». */
export function passiveLine(beast: BeastResponse): string {
  return `+${percent(beast.passive.bonus)} ${effectLabel(beast.passive.effect)}`;
}
