"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { agentApi, AgentRun } from "../lib/api";
import { useHousehold } from "./HouseholdProvider";

const defaultPrompt = "Create a quick vegetarian dinner.";

function titleFor(run: AgentRun) {
  const first = run.events[0]?.replace(/\s+/g, " ").trim();
  return first ? first.slice(0, 54) + (first.length > 54 ? "…" : "") : "New recipe conversation";
}

export function RecipeChatPage() {
  const { household, loading } = useHousehold();
  const [threads, setThreads] = useState<AgentRun[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [prompt, setPrompt] = useState("");
  const [status, setStatus] = useState("");
  const active = useMemo(() => threads.find(thread => thread.id === activeId) ?? null, [threads, activeId]);
  const replace = (next: AgentRun) => setThreads(current => [next, ...current.filter(thread => thread.id !== next.id)]);

  const loadThreads = async () => {
    try {
      if (!household) return;
      const response = await fetch(`${agentApi}/api/v1/agent/runs?householdId=${household.id}`);
      if (!response.ok) throw new Error();
      const next: AgentRun[] = await response.json();
      setThreads(next); setActiveId(current => current && next.some(thread => thread.id === current) ? current : next[0]?.id ?? null);
    } catch { setStatus("Conversation history is unavailable. Start the local stack and try again."); }
  };
  useEffect(() => { loadThreads(); }, [household?.id]);

  const refresh = async (run = active) => {
    if (!run) return;
    try {
      await fetch(`${agentApi}/api/v1/agent/runs/${run.id}/reconcile`, { method: "POST" });
      const response = await fetch(`${agentApi}/api/v1/agent/runs/${run.id}`);
      if (!response.ok) throw new Error();
      replace(await response.json()); setStatus("");
    } catch { setStatus("Could not refresh this conversation."); }
  };

  const start = async () => {
    setActiveId(null); setPrompt(defaultPrompt); setStatus("");
  };
  const send = async (event: FormEvent) => {
    event.preventDefault();
    const message = prompt.trim(); if (!message) return;
    try {
      if (!active) {
        setStatus("Starting conversation…");
        if (!household) throw new Error();
        const response = await fetch(`${agentApi}/api/v1/agent/runs`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ householdId: household.id, prompt: message }) });
        const next = await response.json().catch(() => null);
        if (!response.ok || !next?.id) throw new Error();
        replace(next); setActiveId(next.id); setPrompt(""); setStatus("The recipe assistant is working. Refresh for new messages.");
      } else {
        setStatus("Sending message…");
        const response = await fetch(`${agentApi}/api/v1/agent/runs/${active.id}/messages`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ content: message, delivery: "Queued" }) });
        if (response.status === 409) { setStatus("This conversation already has a message waiting to be handled."); return; }
        if (!response.ok) throw new Error();
        setPrompt(""); await refresh(active); setStatus("Message queued. Refresh for the assistant’s reply.");
      }
    } catch { setStatus("Could not contact the recipe assistant. Check that the local stack is running."); }
  };
  const remove = async (run: AgentRun) => {
    if (!window.confirm(`Delete “${titleFor(run)}”? This cannot be undone.`)) return;
    try {
      const response = await fetch(`${agentApi}/api/v1/agent/runs/${run.id}`, { method: "DELETE" });
      if (!response.ok) throw new Error();
      const next = threads.filter(thread => thread.id !== run.id); setThreads(next); setActiveId(current => current === run.id ? next[0]?.id ?? null : current); setStatus("");
    } catch { setStatus("Could not delete this conversation."); }
  };

  if (loading) return <p className="status">Loading your household…</p>;
  if (!household) return <p className="status">Your household could not be loaded.</p>;
  return <section className="conversation-workspace">
    <aside className="conversation-list" aria-label="Recipe conversations">
      <div className="conversation-list-heading"><div><h2>Conversations</h2><p>Recipe planning history</p></div><button onClick={start}>+ New</button></div>
      <div className="thread-list">{threads.length === 0 && <p className="muted thread-empty">No conversations yet.</p>}{threads.map(thread => <div className={`thread-row ${thread.id === activeId ? "selected" : ""}`} key={thread.id}><button className="thread-select" onClick={() => { setActiveId(thread.id); setPrompt(""); setStatus(""); }}><strong>{titleFor(thread)}</strong><span>{thread.status}{thread.hasPendingQueuedMessage ? " · reply pending" : ""}</span></button><button className="thread-delete" onClick={() => remove(thread)} aria-label={`Delete ${titleFor(thread)}`}>×</button></div>)}</div>
    </aside>
    <div className="conversation-panel">
      <div className="section-heading"><div><h2>{active ? titleFor(active) : "New conversation"}</h2><p className="muted">{active ? "Continue this thread with the recipe assistant." : "Start a new thread to plan a meal."}</p></div>{active && <button className="quiet" onClick={() => refresh()}>Refresh</button>}</div>
      {status && <p role="status" className="status">{status}</p>}
      <div className="message-history" aria-live="polite">{active?.events.length ? active.events.map((message, index) => <div className={`chat-message ${index % 2 ? "assistant-message" : "user-message"}`} key={`${active.id}-${index}`}><span>{index % 2 ? "Recipe assistant" : "You"}</span>{index % 2 ? <div className="markdown-content"><Markdown remarkPlugins={[remarkGfm]} components={{ a: ({ href, children }) => <a href={href} target="_blank" rel="noreferrer">{children}</a> }}>{message}</Markdown></div> : <p>{message}</p>}</div>) : <div className="conversation-empty"><div className="empty-icon">✦</div><h3>{active ? "No messages yet" : "What would you like to make?"}</h3><p>{active ? "Send a follow-up to continue this conversation." : "Ask for ideas using what you have, dietary preferences, or available time."}</p></div>}</div>
      <form className="chat-composer" onSubmit={send}><label htmlFor="recipe-message">Message</label><div><textarea id="recipe-message" value={prompt} onChange={event => setPrompt(event.target.value)} placeholder="e.g. Make it dairy-free and ready in 30 minutes" /><button type="submit" disabled={active?.hasPendingQueuedMessage}>{active ? "Send" : "Start chat"}</button></div></form>
    </div>
  </section>;
}
