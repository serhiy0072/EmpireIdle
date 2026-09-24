import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { useSession } from "../hooks/useSession";
import {
  useChatConversations,
  useChatHistory,
  useSendChat,
  type ChatChannelName,
  type ChatMessageView,
} from "../lib/queries/chat";

const TABS: { channel: ChatChannelName; label: string }[] = [
  { channel: "Server", label: "Світ" },
  { channel: "Clan", label: "Клан" },
  { channel: "Private", label: "Особисті" },
];

const time = (iso: string) => new Date(iso).toLocaleTimeString("uk-UA", { hour: "2-digit", minute: "2-digit" });

/** Одне повідомлення: переклад, якщо є, а оригінал — у підказці. */
function Message({ message }: { message: ChatMessageView }) {
  const translated = message.translatedText != null;

  return (
    <li className={`flex ${message.isOwn ? "justify-end" : "justify-start"}`}>
      <div
        className={`max-w-[80%] rounded-xl px-3 py-1.5 text-sm ${
          message.isOwn ? "bg-emerald-100 text-emerald-950" : "bg-white text-slate-800"
        }`}
        title={translated ? `Оригінал (${message.language}): ${message.text}` : undefined}
      >
        {!message.isOwn && <div className="text-xs font-medium text-slate-500">{message.senderName}</div>}
        <div className="whitespace-pre-wrap break-words">{message.translatedText ?? message.text}</div>
        <div className="mt-0.5 text-right text-[10px] text-slate-400">
          {translated && "перекладено · "}
          {time(message.sentAt)}
        </div>
      </div>
    </li>
  );
}

interface ThreadProps {
  playerId: string;
  channel: ChatChannelName;
  partnerId: string | null;
  emptyText: string;
}

/** Стрічка каналу з полем введення. Нове дописується подією — прокручуємо донизу. */
function Thread({ playerId, channel, partnerId, emptyText }: ThreadProps) {
  const history = useChatHistory(playerId, channel, partnerId);
  const send = useSendChat(playerId);
  const [text, setText] = useState("");
  const bottom = useRef<HTMLLIElement | null>(null);

  const count = history.data?.length ?? 0;

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: "end" });
  }, [count]);

  const submit = () => {
    const trimmed = text.trim();
    if (trimmed === "") return;

    send.mutate({ channel, recipientId: partnerId, text: trimmed }, { onSuccess: () => setText("") });
  };

  return (
    <div className="flex h-[calc(100vh-16rem)] flex-col gap-2">
      <ErrorBanner error={history.error ?? send.error} />

      <ul className="min-h-0 flex-1 space-y-1.5 overflow-y-auto rounded-xl bg-slate-100 p-3">
        {count === 0 && !history.isPending && <li className="text-sm text-slate-500">{emptyText}</li>}
        {history.data?.map((message) => <Message key={message.id} message={message} />)}
        <li ref={bottom} />
      </ul>

      <form
        className="flex gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
      >
        <input
          value={text}
          onChange={(event) => setText(event.target.value)}
          maxLength={300}
          placeholder="Повідомлення…"
          className="min-w-0 flex-1 rounded-lg border border-slate-300 px-3 py-1.5 text-sm"
        />
        <button
          type="submit"
          disabled={send.isPending || text.trim() === ""}
          className="rounded-lg bg-emerald-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          Надіслати
        </button>
      </form>
    </div>
  );
}

/** Список приватних розмов; ?to=id&name=… відкриває нову з конкретним гравцем. */
function PrivateChats({ playerId }: { playerId: string }) {
  const [params, setParams] = useSearchParams();
  const conversations = useChatConversations(playerId);

  const partnerId = params.get("to");
  const partnerName =
    params.get("name") ?? conversations.data?.find((c) => c.partnerId === partnerId)?.partnerName ?? "гравцем";

  if (partnerId !== null) {
    return (
      <div className="space-y-2">
        <button
          type="button"
          onClick={() => setParams({ tab: "Private" }, { replace: true })}
          className="text-sm text-emerald-700 hover:underline"
        >
          ← усі розмови
        </button>
        <p className="text-sm text-slate-600">Розмова з {partnerName}</p>
        <Thread playerId={playerId} channel="Private" partnerId={partnerId} emptyText="Напишіть перше повідомлення." />
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <ErrorBanner error={conversations.error} />
      {conversations.data?.length === 0 && (
        <p className="text-sm text-slate-500">
          Розмов ще немає. Написати гравцеві можна з його картки в клані чи рейтингу.
        </p>
      )}
      <ul className="space-y-1">
        {conversations.data?.map((conversation) => (
          <li key={conversation.partnerId}>
            <button
              type="button"
              onClick={() =>
                setParams({ tab: "Private", to: conversation.partnerId, name: conversation.partnerName }, { replace: true })
              }
              className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-left hover:border-slate-300"
            >
              <div className="text-sm font-medium text-slate-800">{conversation.partnerName}</div>
              <div className="truncate text-xs text-slate-500">
                {conversation.lastMessage.isOwn && "ви: "}
                {conversation.lastMessage.translatedText ?? conversation.lastMessage.text}
              </div>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Чат (GDD §7.3): світ, клан і особисті. Нові повідомлення приходять через SignalR. */
export default function ChatPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const [params, setParams] = useSearchParams();

  const tab = (TABS.find((t) => t.channel === params.get("tab"))?.channel ?? "Server") as ChatChannelName;

  return (
    <div className="space-y-3">
      <h1 className="text-xl font-medium text-slate-800">Чат</h1>

      <nav className="flex gap-1">
        {TABS.map((item) => (
          <button
            key={item.channel}
            type="button"
            onClick={() => setParams({ tab: item.channel }, { replace: true })}
            className={`rounded-lg px-3 py-1 text-sm ${
              tab === item.channel ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"
            }`}
          >
            {item.label}
          </button>
        ))}
      </nav>

      {tab === "Server" && (
        <Thread playerId={playerId} channel="Server" partnerId={null} emptyText="У світі поки тиша. Привітайтеся!" />
      )}
      {tab === "Clan" && (
        <Thread
          playerId={playerId}
          channel="Clan"
          partnerId={null}
          emptyText="Тут пишуть члени вашого клану. Без клану канал порожній."
        />
      )}
      {tab === "Private" && <PrivateChats playerId={playerId} />}
    </div>
  );
}
