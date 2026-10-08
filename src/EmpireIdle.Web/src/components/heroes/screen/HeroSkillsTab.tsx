import { useState } from "react";
import { skillHalfLabel, skillKindLabel, skillPreview, useCatalog, type CatalogHero, type CatalogSkill } from "../../../lib/queries/catalog";
import type { HeroSummary } from "../../../lib/queries/heroes";
import { GameButton, Tile } from "../../game/GameUi";

interface Props {
  hero: HeroSummary;
  config: CatalogHero;
  /** Скільки книг цієї половини («Attack» / «Defense») в інвентарі. */
  books: (half: string) => number;
  busy: boolean;
  onUpgrade: (skillKey: string) => void;
}

const KIND_GLYPHS: Record<string, string> = { Active: "⚔", Passive: "🛡", Periodic: "⟳", Utility: "➶" };

/**
 * Вкладка «Навички» (GDD §6.1): дві колонки — «Атака» й «Захист» (назви половин лишаються наші,
 * рішення 08.10.2026). Під ними — опис обраного вміння, перегляд рівнів і що потрібно для наступного.
 */
export default function HeroSkillsTab({ hero, config, books, busy, onUpgrade }: Props) {
  const catalog = useCatalog();
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const selected = config.skills.find((skill) => skill.key === selectedKey) ?? config.skills[0] ?? null;

  const column = (half: string) => (
    <div className="space-y-3">
      <h3 className="text-center text-sm font-semibold text-sky-100">{skillHalfLabel(half)}</h3>
      {config.skills
        .filter((skill) => skill.half === half)
        .map((skill) => {
          // Рівень рахує сервер: відкриття за рівнем героя й стелю зірок клієнт не дублює
          const level = hero.skillLevels[skill.key] ?? 0;

          return (
            <div key={skill.key} className="mx-auto w-20">
              <Tile
                rarity={config.rank}
                selected={skill.key === selected?.key}
                dimmed={level === 0}
                onClick={() => setSelectedKey(skill.key)}
                title={skill.displayName}
                bottomRight={level === 0 ? `з ${skill.unlockLevel}` : `Рів. ${level}`}
              >
                <span className="text-3xl drop-shadow">{KIND_GLYPHS[skill.kind] ?? "✦"}</span>
              </Tile>
            </div>
          );
        })}
    </div>
  );

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-6">
        {column("Attack")}
        {column("Defense")}
      </div>

      {selected !== null && (
        <SkillDetails
          skill={selected}
          level={hero.skillLevels[selected.key] ?? 0}
          cap={hero.skillLevelCap}
          maxLevel={catalog.maxSkillLevel}
          books={books(selected.half)}
          busy={busy}
          onUpgrade={() => onUpgrade(selected.key)}
        />
      )}
    </div>
  );
}

function SkillDetails({
  skill,
  level,
  cap,
  maxLevel,
  books,
  busy,
  onUpgrade,
}: {
  skill: CatalogSkill;
  level: number;
  cap: number;
  maxLevel: number;
  books: number;
  busy: boolean;
  onUpgrade: () => void;
}) {
  const atMax = level >= maxLevel;
  // Стеля вміння — зірки + 1: наступний рівень відкриває зірка з номером поточного рівня
  const needsStar = level > 0 && !atMax && level >= cap;

  return (
    <div className="space-y-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
      <div className="flex items-baseline justify-between gap-2">
        <h4 className="text-base font-semibold">
          {skill.displayName} <span className="text-sky-200">Рів. {level}</span>
        </h4>
        <span className="text-xs text-sky-200/80">{skillKindLabel(skill.kind)}</span>
      </div>

      <p className="text-sm text-sky-50/90">{skill.description}</p>

      <div className="space-y-1">
        <p className="text-sm font-semibold">Перегляд покращення</p>
        {skillPreview(skill, maxLevel).map((row) => (
          <p key={row.label} className="text-sm text-sky-100/90">
            <span className="text-sky-200/80">{row.label}: </span>
            {row.values.map((value, index) => (
              <span key={index}>
                {index > 0 && "/"}
                <span className={index + 1 === level ? "font-bold text-emerald-300" : ""}>{value}</span>
              </span>
            ))}
          </p>
        ))}
      </div>

      {level === 0 ? (
        <p className="text-center text-sm text-amber-200">Відкриється на {skill.unlockLevel} рівні героя</p>
      ) : atMax ? (
        <p className="text-center text-sm text-emerald-300">Найвищий рівень вміння</p>
      ) : needsStar ? (
        <p className="text-center text-sm font-semibold text-rose-300">Щоб покращити: підніміть до {level} зірки</p>
      ) : (
        <div className="flex items-center justify-center gap-3">
          <span className="text-sm text-sky-100">книг: {books}</span>
          <GameButton onClick={onUpgrade} disabled={busy || books === 0} variant="accent">
            Покращити · 1 книга
          </GameButton>
        </div>
      )}
    </div>
  );
}
