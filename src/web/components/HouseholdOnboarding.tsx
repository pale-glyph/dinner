"use client";

import { useState } from "react";
import { useHousehold } from "@/components/providers/HouseholdProvider";

export function HouseholdOnboarding() {
  const { refresh } = useHousehold();
  const [name, setName] = useState(""); const [token, setToken] = useState(""); const [message, setMessage] = useState(""); const [busy, setBusy] = useState(false);
  const create = async (event: React.FormEvent) => { event.preventDefault(); setBusy(true); setMessage(""); const response = await fetch("/api/households", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name }) }); const body = await response.json().catch(() => ({})); if (!response.ok) setMessage(body.error ?? body.errors?.name?.[0] ?? "Could not create the household."); else await refresh(); setBusy(false); };
  const join = async (event: React.FormEvent) => { event.preventDefault(); setBusy(true); setMessage(""); const invite = token.trim().replace(/^.*\/join\//, ""); const response = await fetch(`/api/households/join/${encodeURIComponent(invite)}`, { method: "POST" }); const body = await response.json().catch(() => ({})); if (!response.ok) setMessage(body.error ?? "That invitation is not valid."); else await refresh(); setBusy(false); };
  return <main className="onboarding"><div className="onboarding-brand"><div className="brand-mark">d</div><span>dinner</span></div><section className="onboarding-card"><p className="eyebrow">Welcome</p><h1>Set up your dinner space</h1><p className="intro">Create a household for the people you cook with, or join one with an invitation QR code.</p><div className="onboarding-options"><form onSubmit={create}><h2>Create a household</h2><label>Household name<input required value={name} onChange={event => setName(event.target.value)} placeholder="e.g. The Parkers" /></label><button disabled={busy}>Create household</button></form><div className="onboarding-divider">or</div><form onSubmit={join}><h2>Join a household</h2><label>Invitation code or link<input required value={token} onChange={event => setToken(event.target.value)} placeholder="Paste the code from a QR scan" /></label><button className="quiet" disabled={busy}>Join household</button></form></div>{message && <p className="status">{message}</p>}</section></main>;
}
