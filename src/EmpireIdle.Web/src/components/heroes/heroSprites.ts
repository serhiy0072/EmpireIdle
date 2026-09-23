/**
 * Маніфест портретів героїв. Ключ — ключ героя з heroes.json, значення — файл
 * за тіром (індекс 0 — тір 1). Порожній маніфест = усі портрети процедурні.
 * Явний список, а не <img onError>: без 404 на кожну картку й миготіння.
 * Конвенція файлів — public/sprites/README.md.
 */
const PORTRAITS: Record<string, string[]> = {
  // Приклад після додавання арту:
  // mage_iselle: ["/sprites/heroes/mage_iselle.png", "/sprites/heroes/mage_iselle_2.png"],
};

/** Портрет для героя або null — тоді малюється процедурний. Тір без файлу відкочується до нижчого. */
export function portraitFor(heroKey: string, tier: number): string | null {
  const variants = PORTRAITS[heroKey];

  if (variants === undefined || variants.length === 0) return null;

  return variants[Math.min(Math.max(tier - 1, 0), variants.length - 1)] ?? null;
}
