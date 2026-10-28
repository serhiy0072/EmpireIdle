import i18n from "i18next";
import resourcesToBackend from "i18next-resources-to-backend";
import { initReactI18next, useTranslation } from "react-i18next";
import uk from "../locales/uk/common.json";
import { queryClient } from "./queryClient";

/** Мови, для яких клієнт має переклад; сервер підтримує той самий набір. */
export const LANGUAGES = ["uk", "en"] as const;
export type Language = (typeof LANGUAGES)[number];

/** Мова, якою написані конфіги: вона вшита в бандл і закриває пропущені ключі інших мов. */
export const DEFAULT_LANGUAGE: Language = "uk";

/** Назви мов їхніми ж мовами — так гравець упізнає свою, навіть не розуміючи поточної. */
export const LANGUAGE_NAMES: Record<Language, string> = { uk: "Українська", en: "English" };

/** Вибір цього пристрою: переживає вихід, щоб наступний вхід і реєстрація почалися тією ж мовою. */
const LANGUAGE_KEY = "ui-language";

declare module "i18next" {
  interface CustomTypeOptions {
    defaultNS: "common";
    resources: { common: typeof uk };
  }
}

function isLanguage(value: string | null | undefined): value is Language {
  return LANGUAGES.includes(value as Language);
}

/** ISO 639-1 з мовного тегу: «en» з «en-GB». */
function primary(tag: string | null | undefined): string | undefined {
  return tag?.split("-")[0]?.toLowerCase();
}

function stored(): string | null {
  try {
    return localStorage.getItem(LANGUAGE_KEY);
  } catch {
    // Приватне вікно без сховища — лишається мова браузера
    return null;
  }
}

function remember(language: Language): void {
  try {
    localStorage.setItem(LANGUAGE_KEY, language);
  } catch {
    // Без сховища мова проживе до перезавантаження — цього досить
  }
}

/** До входу: вибір цього пристрою, інакше перша підтримувана мова браузера, інакше мова конфігів. */
function initialLanguage(): Language {
  const candidates = [stored(), ...navigator.languages, navigator.language].map(primary);

  return candidates.find(isLanguage) ?? DEFAULT_LANGUAGE;
}

/** Поточна мова інтерфейсу — її ж отримує сервер в Accept-Language. */
export function currentLanguage(): Language {
  const language = i18n.resolvedLanguage;

  return isLanguage(language) ? language : DEFAULT_LANGUAGE;
}

/** Поточна мова з підпискою: компонент перемалюється, щойно мову змінять. */
export function useLanguage(): Language {
  const { i18n: instance } = useTranslation();
  const language = instance.resolvedLanguage;

  return isLanguage(language) ? language : DEFAULT_LANGUAGE;
}

/**
 * Перемикає інтерфейс: спершу довантажує переклад, щоб екран не мигнув ключами,
 * потім перечитує всі запити — серверні views перекладені мовою запиту.
 * Непідтримувану мову ігнорує: краще лишитися на поточній, ніж показати ключі.
 */
export async function setLanguage(language: string): Promise<void> {
  if (!isLanguage(language) || language === currentLanguage()) return;

  await i18n.changeLanguage(language);
  remember(language);
  document.documentElement.lang = language;
  void queryClient.invalidateQueries();
}

/** Ініціалізація до першого рендеру: інша мова, ніж вшита, приїжде окремим чанком. */
export async function initI18n(): Promise<void> {
  const language = initialLanguage();

  await i18n
    .use(initReactI18next)
    .use(resourcesToBackend((lng: string, ns: string) => import(`../locales/${lng}/${ns}.json`)))
    .init({
      lng: language,
      fallbackLng: DEFAULT_LANGUAGE,
      supportedLngs: LANGUAGES,
      load: "languageOnly",
      ns: ["common"],
      defaultNS: "common",
      resources: { uk: { common: uk } },
      // Вшита мова — лише частина ресурсів: інші бекенд довантажує на вимогу
      partialBundledLanguages: true,
      // React сам екранує текст
      interpolation: { escapeValue: false },
      react: { useSuspense: false },
    });

  document.documentElement.lang = language;
}
