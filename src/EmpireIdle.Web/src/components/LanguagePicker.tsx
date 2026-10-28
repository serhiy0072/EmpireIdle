import { useTranslation } from "react-i18next";
import { LANGUAGE_NAMES, LANGUAGES, setLanguage, useLanguage } from "../lib/i18n";

/**
 * Мова до входу: лише цей пристрій. Після входу мовою править акаунт —
 * перемикач у меню ⚙ зберігає її на сервері.
 */
export default function LanguagePicker() {
  const { t } = useTranslation();
  const language = useLanguage();

  return (
    <select
      value={language}
      onChange={(event) => void setLanguage(event.target.value)}
      aria-label={t("language")}
      className="rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700"
    >
      {LANGUAGES.map((code) => (
        <option key={code} value={code}>
          {LANGUAGE_NAMES[code]}
        </option>
      ))}
    </select>
  );
}
