import type { BeastResponse } from "../../lib/queries/beasts";

const EFFECT_LABELS: Record<string, string> = {
  Production: "виробіток",
  Attack: "атака",
  Defense: "захист",
  MarchSpeed: "швидкість маршів",
  Carry: "вантажопідйомність",
};

export const percent = (value: number) => `${Math.round(value * 1000) / 10}%`;

/** Що дає пасивка звіра одним рядком: «+21% швидкість маршів». */
export function passiveLine(beast: BeastResponse): string {
  return `+${percent(beast.passive.bonus)} ${EFFECT_LABELS[beast.passive.effect] ?? beast.passive.effect}`;
}
