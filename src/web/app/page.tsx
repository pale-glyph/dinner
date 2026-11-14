"use client";

import { FormEvent, useEffect, useState } from "react";

const pantryApi = process.env.NEXT_PUBLIC_PANTRY_API_URL ?? "http://localhost:18083";
const recipeApi = process.env.NEXT_PUBLIC_RECIPE_API_URL ?? "http://localhost:18084";
const mealPlanApi = process.env.NEXT_PUBLIC_MEAL_PLAN_API_URL ?? "http://localhost:18086";
const householdId = "11111111-1111-1111-1111-111111111111";
type PantryItem = { id: string; name: string; quantity: number; unit: string; location?: string; expiryDate?: string; lowStockThreshold?: number };
type Recipe = { id: string; name: string; mealType: string; totalMinutes: number; dietaryTags: string[] };
type MealPlan = { id: string; status: string; version: number; assignments: { date: string; mealType: string; recipeId: string; locked: boolean }[]; warnings: string[] };

export default function Home() {
  const [items, setItems] = useState<PantryItem[]>([]);
  const [status, setStatus] = useState("Loading pantry…");
  const [name, setName] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [unit, setUnit] = useState("item");
  const [expiryDate, setExpiryDate] = useState("");
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [plan, setPlan] = useState<MealPlan | null>(null);
  const load = async () => {
    try { const response = await fetch(`${pantryApi}/api/v1/pantry/items?householdId=${householdId}`); if (!response.ok) throw new Error(); setItems(await response.json()); setStatus(""); }
    catch { setStatus("Pantry service is unavailable. Start the local stack and try again."); }
  };
  useEffect(() => { load(); fetch(`${recipeApi}/api/v1/recipes?mealType=dinner`).then(r => r.ok ? r.json() : []).then(setRecipes).catch(() => setRecipes([])); }, []);
  const createPlan = async () => {
    if (!recipes.length) { setStatus("Recipes are unavailable, so a plan cannot be created."); return; }
    setStatus("Creating a draft meal plan…"); const start = new Date(); start.setDate(start.getDate() - ((start.getDay() + 6) % 7));
    const assignments = Array.from({ length: 7 }, (_, index) => { const day = new Date(start); day.setDate(day.getDate() + index); return { date: day.toISOString().slice(0, 10), mealType: "dinner", recipeId: recipes[index % recipes.length].id, locked: false }; });
    const response = await fetch(`${mealPlanApi}/api/v1/meal-plans`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ householdId, startDate: start.toISOString().slice(0, 10), servings: 2, assignments, constraints: ["vegetarian-friendly"] }) });
    if (!response.ok) { setStatus("Could not create the plan draft."); return; } setPlan(await response.json()); setStatus("");
  };
  const approvePlan = async () => { if (!plan) return; const response = await fetch(`${mealPlanApi}/api/v1/meal-plans/${plan.id}/approve`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: plan.version }) }); if (!response.ok) { setStatus("This draft changed. Refresh it before approving."); return; } setPlan((await response.json()).plan); };
  const add = async (event: FormEvent) => {
    event.preventDefault(); setStatus("");
    const response = await fetch(`${pantryApi}/api/v1/pantry/items`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ householdId, name, quantity: Number(quantity), unit, expiryDate: expiryDate || null }) });
    if (!response.ok) { setStatus("Could not save that item. Check its quantity and unit."); return; }
    setName(""); setQuantity("1"); setExpiryDate(""); await load();
  };
  const adjust = async (item: PantryItem, kind: "consume" | "restore") => { await fetch(`${pantryApi}/api/v1/pantry/items/${item.id}/adjustments`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ kind, quantity: 1, unit: item.unit }) }); await load(); };
  const remove = async (id: string) => { await fetch(`${pantryApi}/api/v1/pantry/items/${id}`, { method: "DELETE" }); await load(); };
  return (
    <main>
      <p className="eyebrow">Dinner · your kitchen</p>
      <h1>Pantry</h1>
      <p className="intro">Keep track of what you have, use it up, and catch low stock before dinner.</p>
      <nav aria-label="Primary navigation">
        <a href="#pantry">Pantry</a><a aria-disabled="true">Shopping · soon</a><a href="#plan">Plan</a>
      </nav>
      <section id="pantry" className="pantry-grid">
        <div>
          <h2>Add an item</h2>
          <form onSubmit={add}>
            <label>Name<input required value={name} onChange={event => setName(event.target.value)} placeholder="e.g. Chickpeas" /></label>
            <div className="form-row"><label>Quantity<input required min="0.01" step="0.01" type="number" value={quantity} onChange={event => setQuantity(event.target.value)} /></label><label>Unit<select value={unit} onChange={event => setUnit(event.target.value)}><option value="item">items</option><option value="g">grams</option><option value="kg">kilograms</option><option value="ml">millilitres</option><option value="l">litres</option></select></label></div>
            <label>Expiry date <span>(optional)</span><input type="date" value={expiryDate} onChange={event => setExpiryDate(event.target.value)} /></label>
            <button type="submit">Add to pantry</button>
          </form>
        </div>
        <div>
          <div className="section-heading"><h2>On hand</h2><button className="quiet" onClick={load}>Refresh</button></div>
          {status && <p role="status" className="status">{status}</p>}
          {!status && items.length === 0 && <p className="muted">Nothing saved yet.</p>}
          <ul className="items">{items.map(item => <li key={item.id}><div><strong>{item.name}</strong><small>{item.quantity} {item.unit}{item.location ? ` · ${item.location}` : ""}{item.expiryDate ? ` · expires ${item.expiryDate}` : ""}</small></div><div className="actions"><button className="quiet" onClick={() => adjust(item, "consume")}>Use 1</button><button className="quiet" onClick={() => adjust(item, "restore")}>Restore</button><button className="danger" aria-label={`Remove ${item.name}`} onClick={() => remove(item.id)}>Remove</button></div></li>)}</ul>
        </div>
      </section>
      <section id="plan" className="pantry-grid">
        <div><h2>This week&apos;s plan</h2><p className="intro">Create a reviewable dinner draft from the local recipe catalogue. Nothing is committed until you approve it.</p><button onClick={createPlan}>Create seven-day draft</button></div>
        <div>
          <div className="section-heading"><h2>Proposal review</h2>{plan?.status === "Draft" && <button onClick={approvePlan}>Approve plan</button>}</div>
          {!plan && <p className="muted">No meal-plan draft yet.</p>}
          {plan && <><p className="status">{plan.status === "Approved" ? "Approved — ready for the shopping-list step." : "Draft — review it before approval."}</p><ul className="items">{plan.assignments.map((assignment, index) => <li key={`${assignment.date}-${assignment.mealType}`}><div><strong>{assignment.date}</strong><small>{recipes.find(recipe => recipe.id === assignment.recipeId)?.name ?? assignment.recipeId}</small></div><button className="quiet" disabled={plan.status !== "Draft"}>{assignment.locked ? "Locked" : `Swap with ${recipes[(index + 1) % recipes.length]?.name ?? "recipe"}`}</button></li>)}</ul>{plan.warnings.map(warning => <p className="muted" key={warning}>{warning}</p>)}</>}
        </div>
      </section>
    </main>
  );
}
