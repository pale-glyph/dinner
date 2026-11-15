"use client";

import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";

type Household = { id: string; name: string; role: string };
type HouseholdContextValue = { household: Household | null; households: Household[]; loading: boolean; selectHousehold: (id: string) => void; refresh: () => Promise<void> };
const HouseholdContext = createContext<HouseholdContextValue>({ household: null, households: [], loading: true, selectHousehold: () => {}, refresh: async () => {} });

export function HouseholdProvider({ children }: { children: ReactNode }) {
  const [households, setHouseholds] = useState<Household[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    const response = await fetch("/api/account/me", { cache: "no-store" });
    if (!response.ok) { setHouseholds([]); return; }
    const data = await response.json();
    const next = (data.households ?? []) as Household[];
    setHouseholds(next);
    setSelectedId(current => current && next.some(item => item.id === current) ? current : next[0]?.id ?? null);
  }, []);
  useEffect(() => { refresh().catch(() => setHouseholds([])).finally(() => setLoading(false)); }, []);
  const selectHousehold = (id: string) => setSelectedId(id);
  const household = households.find(item => item.id === selectedId) ?? null;

  return <HouseholdContext.Provider value={{ household, households, loading, selectHousehold, refresh }}>{children}</HouseholdContext.Provider>;
}

export function useHousehold() {
  return useContext(HouseholdContext);
}
