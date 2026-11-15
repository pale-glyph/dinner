import type { FormEvent } from "react";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { titleFor } from "@/features/recipe-chat/hooks/useRecipeConversations";
import type { AgentRun } from "@/types/api";

type ConversationPanelProps = {
  active: AgentRun | null;
  prompt: string;
  status: string;
  onPromptChange: (prompt: string) => void;
  onRefresh: () => void;
  onSubmit: (event: FormEvent) => void;
};

export function ConversationPanel({
  active,
  prompt,
  status,
  onPromptChange,
  onRefresh,
  onSubmit,
}: ConversationPanelProps) {
  return (
    <div className="conversation-panel">
      <div className="section-heading">
        <div>
          <h2>{active ? titleFor(active) : "New conversation"}</h2>
          <p className="muted">
            {active
              ? "Continue this thread with the recipe assistant."
              : "Start a new thread to plan a meal."}
          </p>
        </div>
        {active && (
          <button className="quiet" onClick={onRefresh}>
            Refresh
          </button>
        )}
      </div>
      {status && (
        <p role="status" className="status">
          {status}
        </p>
      )}
      <div className="message-history" aria-live="polite">
        {active?.events.length ? (
          active.events.map((message, index) => (
            <div
              className={`chat-message ${index % 2 ? "assistant-message" : "user-message"}`}
              key={`${active.id}-${index}`}
            >
              <span>{index % 2 ? "Recipe assistant" : "You"}</span>
              {index % 2 ? (
                <div className="markdown-content">
                  <Markdown
                    remarkPlugins={[remarkGfm]}
                    components={{
                      a: ({ href, children }) => (
                        <a href={href} target="_blank" rel="noreferrer">
                          {children}
                        </a>
                      ),
                    }}
                  >
                    {message}
                  </Markdown>
                </div>
              ) : (
                <p>{message}</p>
              )}
            </div>
          ))
        ) : (
          <div className="conversation-empty">
            <div className="empty-icon">✦</div>
            <h3>
              {active ? "No messages yet" : "What would you like to make?"}
            </h3>
            <p>
              {active
                ? "Send a follow-up to continue this conversation."
                : "Ask for ideas using what you have, dietary preferences, or available time."}
            </p>
          </div>
        )}
      </div>
      <form className="chat-composer" onSubmit={onSubmit}>
        <label htmlFor="recipe-message">Message</label>
        <div>
          <textarea
            id="recipe-message"
            value={prompt}
            onChange={(event) => onPromptChange(event.target.value)}
            placeholder="e.g. Make it dairy-free and ready in 30 minutes"
          />
          <button type="submit" disabled={active?.hasPendingQueuedMessage}>
            {active ? "Send" : "Start chat"}
          </button>
        </div>
      </form>
    </div>
  );
}
