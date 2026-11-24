import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { useSession } from "../hooks/useSession";
import {
  useChatConversations,
  useChatHistory,
  useSendChat,
  type ChatChannelName,
  type ChatMessageView,
} from "../lib/queries/chat";
import { formatClock } from "../lib/format";
import { GameScreen, GameTabs } from "../components/game/GameUi";
import { t } from "i18next";

const TABS: { channel: ChatChannelName; label: "world" | "clan" | "private" }[] = [
  { channel: "Server", label: "world" },
  { channel: "Clan", label: "clan" },
  { channel: "Private", label: "private" },
];

const time = (iso: string) => formatClock(iso);

/** Одне повідомлення: переклад, якщо є, а оригінал — у підказці. */
function Message({ message }: { message: ChatMessageView }) {
  const translated = message.translatedText != null;

  return (
    <li className={`flex ${message.isOwn ? "justify-end" : "justify-start"}`}>
      <div
        className={`max-w-[80%] rounded-xl px-3 py-1.5 text-sm ${
          message.isOwn ? "bg-emerald-500/25 text-emerald-950" : "bg-[#1d3b6f] text-white"
        }`}
        title={translated ? t("social:chat.original", { language: message.language, text: message.text }) : undefined}
      >
        {!message.isOwn && <div className="text-xs font-medium text-sky-100/70">{message.senderName}</div>}
        <div className="whitespace-pre-wrap break-words">{message.translatedText ?? message.text}</div>
        <div className="mt-0.5 text-right text-[10px] text-sky-100/50">
          {translated && `${t("social:chat.translated")} · `}
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

      <ul className="min-h-0 flex-1 space-y-1.5 overflow-y-auto rounded-xl bg-white/10 p-3">
        {count === 0 && !history.isPending && <li className="text-sm text-sky-100/70">{emptyText}</li>}
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
          placeholder={t("social:chat.placeholder")}
          className="min-w-0 flex-1 rounded-lg border border-[#4a72b0] px-3 py-1.5 text-sm"
        />
        <button
          type="submit"
          disabled={send.isPending || text.trim() === ""}
          className="rounded-lg bg-emerald-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          {t("social:chat.send")}
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
    params.get("name") ?? conversations.data?.find((c) => c.partnerId === partnerId)?.partnerName ?? t("social:chat.aPlayer");

  if (partnerId !== null) {
    return (
      <div className="space-y-2">
        <button
          type="button"
          onClick={() => setParams({ tab: "Private" }, { replace: true })}
          className="text-sm text-emerald-200 hover:underline"
        >
          {t("social:chat.allConversations")}
        </button>
        <p className="text-sm text-slate-200">{t("social:chat.conversationWith", { name: partnerName })}</p>
        <Thread playerId={playerId} channel="Private" partnerId={partnerId} emptyText={t("social:chat.firstMessage")} />
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <ErrorBanner error={conversations.error} />
      {conversations.data?.length === 0 && (
        <p className="text-sm text-sky-100/70">
          {t("social:chat.noConversations")}
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
              className="w-full rounded-lg border border-[#335c9a] bg-[#1d3b6f] px-3 py-2 text-left hover:border-[#6a93d0]"
            >
              <div className="text-sm font-medium text-white">{conversation.partnerName}</div>
              <div className="truncate text-xs text-sky-100/70">
                {conversation.lastMessage.isOwn && `${t("social:chat.you")}: `}
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
  const navigate = useNavigate();

  const tab = TABS.find((item) => item.channel === params.get("tab"))?.channel ?? "Server";

  return (
    <GameScreen
      title={t("social:chat.title")}
      actions={
        <>
          {/* Чат розкривається зі смужки над панеллю — згортаємо туди, звідки прийшли */}
          <button type="button" onClick={() => navigate(-1)} className="text-sm text-emerald-200 hover:underline">
            {t("social:chat.collapse")}
          </button>
        </>
      }
    >
      <div className="space-y-3">
        <GameTabs
          tabs={TABS.map((item) => ({ key: item.channel, label: t(`social:chat.channel.${item.label}`) }))}
          value={tab}
          onChange={(channel) => setParams({ tab: channel }, { replace: true })}
        />

        {tab === "Server" && (
          <Thread playerId={playerId} channel="Server" partnerId={null} emptyText={t("social:chat.worldEmpty")} />
        )}
        {tab === "Clan" && (
          <Thread
            playerId={playerId}
            channel="Clan"
            partnerId={null}
            emptyText={t("social:chat.clanEmpty")}
          />
        )}
        {tab === "Private" && <PrivateChats playerId={playerId} />}
      </div>
    </GameScreen>
  );
}
