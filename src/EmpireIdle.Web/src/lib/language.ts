/**
 * Мова гравця до входу: те, що він обрав на цьому пристрої, інакше мова браузера.
 * Реєстрація шле її на сервер, і акаунт одразу говорить тією мовою, яку гравець уже бачив.
 * Непідтримувану мову сервер тихо замінить на свою за замовчуванням.
 */
const GUEST_LANGUAGE_KEY = "ui-language";

function stored(): string | null {
  try {
    return localStorage.getItem(GUEST_LANGUAGE_KEY);
  } catch {
    // Приватне вікно без сховища — лишається мова браузера
    return null;
  }
}

/** ISO 639-1 («en» з «en-GB») або undefined, якщо браузер мови не назвав. */
export function guestLanguage(): string | undefined {
  const tag = stored() ?? navigator.languages.at(0) ?? navigator.language;

  return tag ? tag.split("-")[0]?.toLowerCase() : undefined;
}
