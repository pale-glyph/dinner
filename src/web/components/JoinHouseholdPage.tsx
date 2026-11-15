"use client";

import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useHousehold } from "@/components/providers/HouseholdProvider";

export function JoinHouseholdPage() {
  const { token } = useParams(); const navigate = useNavigate(); const { refresh } = useHousehold(); const [message, setMessage] = useState("Joining household…");
  useEffect(() => { if (!token) return; fetch(`/api/households/join/${encodeURIComponent(token)}`, { method: "POST" }).then(async response => { const body = await response.json().catch(() => ({})); if (!response.ok) throw new Error(body.error ?? "This invitation is not valid."); await refresh(); setMessage(`You joined ${body.name}.`); setTimeout(() => navigate("/pantry", { replace: true }), 900); }).catch(error => setMessage(error.message)); }, [token, refresh, navigate]);
  return <main className="join-page"><section><p className="eyebrow">Household invitation</p><h1>{message}</h1><p className="intro">You can belong to more than one household and switch between them anytime.</p></section></main>;
}
