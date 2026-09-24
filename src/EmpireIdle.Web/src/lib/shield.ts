/** Кінець щита після падіння міста — так, як його бачить гравець: день і година. */
export function shieldUntilLabel(iso: string): string {
  return new Date(iso).toLocaleString("uk-UA", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}

/** Чи діє щит зараз. Сервер віддає лише чинний, але сторінка могла простояти відкритою. */
export function isShieldActive(iso: string | null | undefined): iso is string {
  return iso != null && new Date(iso).getTime() > Date.now();
}
