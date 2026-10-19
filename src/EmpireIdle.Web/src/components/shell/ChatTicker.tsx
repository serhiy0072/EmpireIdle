import { useRef, useState, type PointerEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useChatConversations, useChatHistory, type ChatChannelName } from "../../lib/queries/chat";

const CHANNELS: { channel: ChatChannelName; label: string }[] = [
  { channel: "Server", label: "Світ" },
  { channel: "Clan", label: "Клан" },
  { channel: "Private", label: "Особисті" },
];

/** Скільки пікселів по горизонталі — уже свайп, а не тап. */
const SWIPE_PX = 40;

/** Скільки останніх рядків видно в смужці. */
const LINES = 2;

interface Line {
  id: string;
  name: string;
  text: string;
}

/**
 * Чат над нижньою панеллю (рішення 20.10.2026): напівпрозора смужка з останніми повідомленнями.
 * Свайп уліво/вправо перемикає світ, клан і особисті; тап розкриває повний чат на цьому каналі.
 */
export default function ChatTicker({ playerId }: { playerId: string }) {
  const navigate = useNavigate();
  const [index, setIndex] = useState(0);
  const start = useRef<{ x: number; y: number } | null>(null);

  const server = useChatHistory(playerId, "Server", null);
  const clan = useChatHistory(playerId, "Clan", null);
  const conversations = useChatConversations(playerId);

  const current = CHANNELS[index] ?? CHANNELS[0]!;
  const step = (delta: number) => setIndex((value) => (value + delta + CHANNELS.length) % CHANNELS.length);

  const fromHistory = (messages: typeof server.data): Line[] =>
    (messages ?? []).slice(-LINES).map((message) => ({
      id: message.id,
      name: message.isOwn ? "ви" : message.senderName,
      text: message.translatedText ?? message.text,
    }));

  const lines: Line[] =
    current.channel === "Server"
      ? fromHistory(server.data)
      : current.channel === "Clan"
        ? fromHistory(clan.data)
        : (conversations.data ?? []).slice(0, LINES).map((conversation) => ({
            id: conversation.partnerId,
            name: conversation.partnerName,
            text: `${conversation.lastMessage.isOwn ? "ви: " : ""}${conversation.lastMessage.translatedText ?? conversation.lastMessage.text}`,
          }));

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    start.current = { x: event.clientX, y: event.clientY };
  };

  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    const from = start.current;
    start.current = null;
    if (from === null) return;

    const dx = event.clientX - from.x;
    const dy = event.clientY - from.y;

    if (Math.abs(dx) >= SWIPE_PX && Math.abs(dx) > Math.abs(dy)) {
      // Свайп уліво — наступний канал, як гортання сторінок
      step(dx < 0 ? 1 : -1);
      return;
    }

    if (Math.abs(dx) < 8 && Math.abs(dy) < 8) navigate(`/chat?tab=${current.channel}`);
  };

  const arrow = (delta: number, label: string, glyph: string) => (
    <button
      type="button"
      aria-label={label}
      onPointerDown={(event) => event.stopPropagation()}
      onPointerUp={(event) => event.stopPropagation()}
      onClick={() => step(delta)}
      className="shrink-0 px-1 text-lg font-bold text-sky-100/70 hover:text-white"
    >
      {glyph}
    </button>
  );

  return (
    <div
      role="button"
      aria-label={`Чат: ${current.label}. Торкніться, щоб розкрити; гортайте вбік, щоб змінити канал`}
      onPointerDown={onPointerDown}
      onPointerUp={onPointerUp}
      onPointerCancel={() => (start.current = null)}
      className="pointer-events-auto flex w-[min(26rem,calc(100vw-6rem))] touch-pan-y select-none items-center gap-1 rounded-t-xl bg-[#0f2147]/55 px-1 py-1 text-white shadow-lg backdrop-blur-sm [text-shadow:0_1px_1px_#000a]"
    >
      {arrow(-1, "Попередній канал", "‹")}
      <div className="min-w-0 flex-1 cursor-pointer">
        <div className="flex items-center gap-2 text-[11px] font-bold text-amber-200">
          {current.label}
          <span className="flex gap-1">
            {CHANNELS.map((item, i) => (
              <span key={item.channel} className={`h-1.5 w-1.5 rounded-full ${i === index ? "bg-amber-200" : "bg-white/35"}`} />
            ))}
          </span>
        </div>
        {lines.length === 0 ? (
          <p className="truncate text-xs text-sky-100/70">
            {current.channel === "Private" ? "Особистих розмов ще немає" : "Поки тиша — торкніться, щоб написати"}
          </p>
        ) : (
          lines.map((line) => (
            <p key={line.id} className="truncate text-xs">
              <span className="font-semibold text-sky-200">{line.name}:</span> {line.text}
            </p>
          ))
        )}
      </div>
      {arrow(1, "Наступний канал", "›")}
    </div>
  );
}
