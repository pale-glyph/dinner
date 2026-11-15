import { useHousehold } from "@/components/providers/HouseholdProvider";
import { ConversationList } from "@/features/recipe-chat/components/ConversationList";
import { ConversationPanel } from "@/features/recipe-chat/components/ConversationPanel";
import { useRecipeConversations } from "@/features/recipe-chat/hooks/useRecipeConversations";

export function RecipeChatPage() {
  const { household, loading } = useHousehold();
  const conversations = useRecipeConversations(household?.id);
  if (loading) return <p className="status">Loading your household…</p>;
  if (!household)
    return <p className="status">Your household could not be loaded.</p>;
  return (
    <section className="conversation-workspace">
      <ConversationList
        threads={conversations.threads}
        activeId={conversations.activeId}
        onStart={conversations.start}
        onSelect={conversations.select}
        onRemove={conversations.remove}
      />
      <ConversationPanel
        active={conversations.active}
        prompt={conversations.prompt}
        status={conversations.status}
        onPromptChange={conversations.setPrompt}
        onRefresh={() => {
          void conversations.refresh();
        }}
        onSubmit={conversations.send}
      />
    </section>
  );
}
