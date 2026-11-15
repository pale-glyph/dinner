import { titleFor } from "@/features/recipe-chat/hooks/useRecipeConversations";
import type { AgentRun } from "@/types/api";

type ConversationListProps = {
  threads: AgentRun[];
  activeId: string | null;
  onStart: () => void;
  onSelect: (id: string) => void;
  onRemove: (run: AgentRun) => void;
};

export function ConversationList({
  threads,
  activeId,
  onStart,
  onSelect,
  onRemove,
}: ConversationListProps) {
  return (
    <aside className="conversation-list" aria-label="Recipe conversations">
      <div className="conversation-list-heading">
        <div>
          <h2>Conversations</h2>
          <p>Recipe planning history</p>
        </div>
        <button onClick={onStart}>+ New</button>
      </div>
      <div className="thread-list">
        {threads.length === 0 && (
          <p className="muted thread-empty">No conversations yet.</p>
        )}
        {threads.map((thread) => (
          <div
            className={`thread-row ${thread.id === activeId ? "selected" : ""}`}
            key={thread.id}
          >
            <button
              className="thread-select"
              onClick={() => onSelect(thread.id)}
            >
              <strong>{titleFor(thread)}</strong>
              <span>
                {thread.status}
                {thread.hasPendingQueuedMessage ? " · reply pending" : ""}
              </span>
            </button>
            <button
              className="thread-delete"
              onClick={() => onRemove(thread)}
              aria-label={`Delete ${titleFor(thread)}`}
            >
              ×
            </button>
          </div>
        ))}
      </div>
    </aside>
  );
}
