import { useCallback, useEffect, useState } from "react";
import { pantryApi } from "@/lib/api";
import type { PantryItem } from "@/types/api";

export function usePantry(householdId?: string) {
  const [items, setItems] = useState<PantryItem[]>([]);
  const [status, setStatus] = useState("Loading pantry…");

  const load = useCallback(async () => {
    if (!householdId) return;
    try {
      const response = await fetch(
        `${pantryApi}/api/v1/pantry/items?householdId=${householdId}`,
      );
      if (!response.ok) throw new Error();
      setItems(await response.json());
      setStatus("");
    } catch {
      setStatus(
        "Pantry service is unavailable. Start the local stack and try again.",
      );
    }
  }, [householdId]);

  useEffect(() => {
    void load();
  }, [load]);

  const addItem = async (input: {
    name: string;
    quantity: number;
    unit: string;
    expiryDate: string;
  }) => {
    if (!householdId) return false;
    const response = await fetch(`${pantryApi}/api/v1/pantry/items`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        householdId,
        ...input,
        expiryDate: input.expiryDate || null,
      }),
    });
    if (!response.ok) {
      setStatus("Could not save that item. Check its quantity and unit.");
      return false;
    }
    await load();
    return true;
  };

  const adjustItem = async (item: PantryItem, kind: "consume" | "restore") => {
    await fetch(`${pantryApi}/api/v1/pantry/items/${item.id}/adjustments`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ kind, quantity: 1, unit: item.unit }),
    });
    await load();
  };

  const removeItem = async (id: string) => {
    await fetch(`${pantryApi}/api/v1/pantry/items/${id}`, { method: "DELETE" });
    await load();
  };

  return { items, status, load, addItem, adjustItem, removeItem };
}
