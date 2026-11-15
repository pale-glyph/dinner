"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";

type Household = { id: string; name: string; role: string };
const HouseholdContext = createContext<{ household: Household | null; loading: boolean }>({ household: null, loading: true });

export function HouseholdProvider({ children }: { children: ReactNode }) {
  const [household, setHousehold] = useState<Household | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch("/api/account/me").then(response => response.ok ? response.json() : Promise.reject()).then(data => setHousehold(data.household)).catch(() => setHousehold(null)).finally(() => setLoading(false));
  }, []);

  return <HouseholdContext.Provider value={{ household, loading }}>{children}</HouseholdContext.Provider>;
}

export function useHousehold() {
  return useContext(HouseholdContext);
}
