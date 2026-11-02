import { Trans } from "react-i18next";
/**
 * Голод (рішення 13.10.2026; м'ясо й корм — 20.10.2026). Наслідки й вихід — одним рядком,
 * щоб гравець одразу знав, що робити. Голод села й голод звірів — окремі.
 */
export default function HungerBanner({ kind }: { kind: "village" | "beasts" }) {
  return (
    <div role="alert" className="rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800">
      {kind === "village" ? (
        <>
          <Trans ns="village" i18nKey="hunger.village" components={{ b: <span className="font-medium" /> }} />
        </>
      ) : (
        <>
          <Trans ns="village" i18nKey="hunger.beasts" components={{ b: <span className="font-medium" /> }} />
        </>
      )}
    </div>
  );
}
