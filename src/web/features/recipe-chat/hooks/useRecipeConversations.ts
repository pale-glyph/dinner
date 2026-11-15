import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type FormEvent,
} from "react";
import { agentApi } from "@/lib/api";
import type { AgentRun } from "@/types/api";

const defaultPrompt = "Create a quick vegetarian dinner.";

export function useRecipeConversations(householdId?: string) {
  const [threads, setThreads] = useState<AgentRun[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [prompt, setPrompt] = useState("");
  const [status, setStatus] = useState("");
  const active = useMemo(
    () => threads.find((thread) => thread.id === activeId) ?? null,
    [threads, activeId],
  );
  const replace = useCallback(
    (next: AgentRun) =>
      setThreads((current) => [
        next,
        ...current.filter((thread) => thread.id !== next.id),
      ]),
    [],
  );

  const loadThreads = useCallback(async () => {
    if (!householdId) return;
    try {
      const response = await fetch(
        `${agentApi}/api/v1/agent/runs?householdId=${householdId}`,
      );
      if (!response.ok) throw new Error();
      const next: AgentRun[] = await response.json();
      setThreads(next);
      setActiveId((current) =>
        current && next.some((thread) => thread.id === current)
          ? current
          : (next[0]?.id ?? null),
      );
    } catch {
      setStatus(
        "Conversation history is unavailable. Start the local stack and try again.",
      );
    }
  }, [householdId]);

  useEffect(() => {
    void loadThreads();
  }, [loadThreads]);

  const refresh = useCallback(
    async (run = active) => {
      if (!run) return;
      try {
        await fetch(`${agentApi}/api/v1/agent/runs/${run.id}/reconcile`, {
          method: "POST",
        });
        const response = await fetch(`${agentApi}/api/v1/agent/runs/${run.id}`);
        if (!response.ok) throw new Error();
        replace(await response.json());
        setStatus("");
      } catch {
        setStatus("Could not refresh this conversation.");
      }
    },
    [active, replace],
  );

  const start = () => {
    setActiveId(null);
    setPrompt(defaultPrompt);
    setStatus("");
  };
  const select = (id: string) => {
    setActiveId(id);
    setPrompt("");
    setStatus("");
  };

  const send = async (event: FormEvent) => {
    event.preventDefault();
    const message = prompt.trim();
    if (!message || !householdId) return;
    try {
      if (!active) {
        setStatus("Starting conversation…");
        const response = await fetch(`${agentApi}/api/v1/agent/runs`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ householdId, prompt: message }),
        });
        const next = await response.json().catch(() => null);
        if (!response.ok || !next?.id) throw new Error();
        replace(next);
        setActiveId(next.id);
        setPrompt("");
        setStatus("The recipe assistant is working. Refresh for new messages.");
        return;
      }
      setStatus("Sending message…");
      const response = await fetch(
        `${agentApi}/api/v1/agent/runs/${active.id}/messages`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ content: message, delivery: "Queued" }),
        },
      );
      if (response.status === 409) {
        setStatus(
          "This conversation already has a message waiting to be handled.",
        );
        return;
      }
      if (!response.ok) throw new Error();
      setPrompt("");
      await refresh(active);
      setStatus("Message queued. Refresh for the assistant’s reply.");
    } catch {
      setStatus(
        "Could not contact the recipe assistant. Check that the local stack is running.",
      );
    }
  };

  const remove = async (run: AgentRun) => {
    if (!window.confirm(`Delete “${titleFor(run)}”? This cannot be undone.`))
      return;
    try {
      const response = await fetch(`${agentApi}/api/v1/agent/runs/${run.id}`, {
        method: "DELETE",
      });
      if (!response.ok) throw new Error();
      const next = threads.filter((thread) => thread.id !== run.id);
      setThreads(next);
      setActiveId((current) =>
        current === run.id ? (next[0]?.id ?? null) : current,
      );
      setStatus("");
    } catch {
      setStatus("Could not delete this conversation.");
    }
  };

  return {
    threads,
    active,
    activeId,
    prompt,
    status,
    setPrompt,
    start,
    select,
    send,
    remove,
    refresh,
  };
}

export function titleFor(run: AgentRun) {
  const first = run.events[0]?.replace(/\s+/g, " ").trim();
  return first
    ? first.slice(0, 54) + (first.length > 54 ? "…" : "")
    : "New recipe conversation";
}
