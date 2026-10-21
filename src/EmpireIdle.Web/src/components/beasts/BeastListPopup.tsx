import { useHotkeys } from "../../hooks/useHotkeys";
import type { BeastResponse } from "../../lib/queries/beasts";
import { BeastTiles } from "./BeastPanels";

interface Props {
  beasts: BeastResponse[];
  name: (beastKey: string) => string;
  onPick: (beastKey: string) => void;
  onClose: () => void;
}

/** «Список звірів» поверх загону чи екрана звіра: усі звірі плитками; клік поза вікном чи Esc закриває. */
export default function BeastListPopup({ beasts, name, onPick, onClose }: Props) {
  useHotkeys({ Escape: onClose }, { modal: true });

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Список звірів"
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4"
    >
      <div
        onClick={(event) => event.stopPropagation()}
        className="w-full max-w-lg space-y-3 rounded-2xl bg-white p-4 shadow-2xl ring-2 ring-sky-300"
      >
        <div className="flex items-center">
          <h2 className="flex-1 text-lg font-semibold text-slate-800">Список звірів</h2>
          <button type="button" aria-label="Закрити" onClick={onClose} className="text-xl font-bold text-slate-500 hover:text-slate-800">
            ✕
          </button>
        </div>
        <BeastTiles beasts={beasts} name={name} onPick={onPick} />
      </div>
    </div>
  );
}
