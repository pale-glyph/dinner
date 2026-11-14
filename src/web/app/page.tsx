"use client";

import { FormEvent, useEffect, useState } from "react";

const pantryApi = process.env.NEXT_PUBLIC_PANTRY_API_URL ?? "http://localhost:18083";
const recipeApi = process.env.NEXT_PUBLIC_RECIPE_API_URL ?? "http://localhost:18084";
const agentApi = process.env.NEXT_PUBLIC_AGENT_GATEWAY_API_URL ?? "http://localhost:18081";
const householdId = "11111111-1111-1111-1111-111111111111";
type PantryItem = { id: string; name: string; quantity: number; unit: string; location?: string; expiryDate?: string; lowStockThreshold?: number };
type Ingredient = { name: string; quantity: number; unit: string; nutritionProductId?: string | null; unresolved?: boolean };
type Recipe = { id: string; title: string; description?: string; instructions: string; totalMinutes: number; servings: number; tags: string[]; ingredients: Ingredient[]; revision: number };
type Proposal = { id: string; recipe?: { title: string }; kind: string };
type AgentRun = { id: string; status: string; events: string[]; threadId?: string };

export default function Home() {
  const [items, setItems] = useState<PantryItem[]>([]);
  const [status, setStatus] = useState("Loading pantry…");
  const [name, setName] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [unit, setUnit] = useState("item");
  const [expiryDate, setExpiryDate] = useState("");
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [prompt, setPrompt] = useState("Create a quick vegetarian dinner with the pantry ingredients.");
  const [run, setRun] = useState<AgentRun | null>(null);
  const [proposal, setProposal] = useState<Proposal | null>(null);
  const [pendingProposals, setPendingProposals] = useState<Proposal[]>([]);
  const [recipeTitle, setRecipeTitle] = useState("");
  const [instructions, setInstructions] = useState("");
  const [ingredients, setIngredients] = useState("Tinned tomatoes|2|item\nChickpeas|1|item");
  const [editing, setEditing] = useState<Recipe | null>(null);
  const load = async () => {
    try { const response = await fetch(`${pantryApi}/api/v1/pantry/items?householdId=${householdId}`); if (!response.ok) throw new Error(); setItems(await response.json()); setStatus(""); }
    catch { setStatus("Pantry service is unavailable. Start the local stack and try again."); }
  };
  const loadRecipes = async () => { const response = await fetch(`${recipeApi}/api/v1/recipes?householdId=${householdId}`); if (response.ok) setRecipes(await response.json()); };
  const loadProposals = async () => { const response = await fetch(`${recipeApi}/api/v1/recipes/proposals?householdId=${householdId}`); if (response.ok) setPendingProposals(await response.json()); };
  useEffect(() => { load(); loadRecipes().catch(() => setStatus("Recipe service is unavailable.")); loadProposals().catch(() => undefined); }, []);
  const chat = async (event: FormEvent) => { event.preventDefault(); setStatus("Starting agent conversation…"); const response = await fetch(`${agentApi}/api/v1/agent/runs`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ householdId, prompt }) }); if (!response.ok) { setStatus("Could not start the agent conversation."); return; } const next = await response.json(); setRun(next); setStatus(next.status === "AwaitingPlatformConfiguration" ? "Agent Platform is not configured yet; the recipe editor remains available." : "Agent is working. Refresh the run to see new messages."); };
  const refreshRun = async () => { if (!run) return; await fetch(`${agentApi}/api/v1/agent/runs/${run.id}/reconcile`, { method: "POST" }); const response = await fetch(`${agentApi}/api/v1/agent/runs/${run.id}`); if (response.ok) setRun(await response.json()); await loadProposals(); };
  const toInput = () => ({ householdId, title: recipeTitle, instructions, totalMinutes: 30, servings: 2, tags: ["dinner"], ingredients: ingredients.split("\n").filter(Boolean).map(line => { const [name, quantity, unit] = line.split("|"); return { name: name?.trim(), quantity: Number(quantity), unit: unit?.trim() }; }) });
  const propose = async (event: FormEvent) => { event.preventDefault(); const input = toInput(); const endpoint = editing ? "propose_recipe_update" : "propose_recipe_create"; const body = editing ? { recipeId: editing.id, expectedRevision: editing.revision, recipe: input } : input; const response = await fetch(`${recipeApi}/mcp/tools/${endpoint}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) }); if (!response.ok) { setStatus("The recipe draft needs a title, instructions, servings, and valid ingredients."); return; } const result = await response.json(); setProposal(result.structuredContent); setStatus("Recipe proposal ready for your approval."); };
  const approveProposal = async () => { if (!proposal) return; const response = await fetch(`${recipeApi}/mcp/tools/apply_approved_recipe_proposal`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ proposalId: proposal.id, approvalReference: `ui:${proposal.id}`, idempotencyKey: `ui:${proposal.id}` }) }); if (!response.ok) { setStatus("Could not apply that proposal. It may have been changed or already applied."); return; } setProposal(null); setEditing(null); setRecipeTitle(""); setInstructions(""); await loadRecipes(); await loadProposals(); setStatus("Recipe saved."); };
  const beginEdit = (recipe: Recipe) => { setEditing(recipe); setRecipeTitle(recipe.title); setInstructions(recipe.instructions); setIngredients(recipe.ingredients.map(i => `${i.name}|${i.quantity}|${i.unit}`).join("\n")); window.location.hash = "recipe-editor"; };
  const deleteRecipe = async (recipe: Recipe) => { if (!window.confirm(`Delete ${recipe.title}? This requires explicit confirmation.`)) return; const response = await fetch(`${recipeApi}/api/v1/recipes/${recipe.id}?householdId=${householdId}&expectedRevision=${recipe.revision}`, { method: "DELETE" }); if (!response.ok) { setStatus("Could not delete this recipe because it changed."); return; } await loadRecipes(); setStatus("Recipe deleted."); };
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
        <a href="#pantry">Pantry</a><a href="#assistant">Recipe chat</a><a href="#recipes">Recipes</a>
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
      <section id="assistant" className="pantry-grid"><div><h2>Recipe chat</h2><p className="intro">Ask the agent for a recipe. It can use your pantry and preferences, but it cannot save changes without your approval.</p><form onSubmit={chat}><label>Message<textarea value={prompt} onChange={event => setPrompt(event.target.value)} /></label><button>Send to recipe agent</button></form></div><div><div className="section-heading"><h2>Conversation</h2>{run && <button className="quiet" onClick={refreshRun}>Refresh</button>}</div>{!run && <p className="muted">Start a conversation to see agent messages here.</p>}{run && <><p className="status">{run.status}</p><ul className="items">{run.events.map((message, index) => <li key={index}>{message}</li>)}</ul></>}</div></section>
      <section id="recipe-editor" className="pantry-grid"><div><h2>{editing ? "Edit recipe" : "Recipe draft"}</h2><form onSubmit={propose}><label>Title<input required value={recipeTitle} onChange={event => setRecipeTitle(event.target.value)} placeholder="e.g. Smoky chickpea pasta" /></label><label>Instructions<textarea required value={instructions} onChange={event => setInstructions(event.target.value)} placeholder="Cook the pasta…" /></label><label>Ingredients <span>one per line: name|quantity|unit</span><textarea required value={ingredients} onChange={event => setIngredients(event.target.value)} /></label><button>{editing ? "Propose update" : "Propose recipe"}</button></form></div><div><div className="section-heading"><h2>Proposal review</h2>{proposal && <button onClick={approveProposal}>Approve and save</button>}</div>{proposal ? <p className="status">A {proposal.kind} proposal is awaiting approval. Saving creates an audit reference.</p> : <><p className="muted">Generated and edited recipes stay drafts until you approve them.</p>{pendingProposals.map(item => <p key={item.id}><button className="quiet" onClick={() => setProposal(item)}>Review agent {item.kind} proposal{item.recipe?.title ? `: ${item.recipe.title}` : ""}</button></p>)}</>}</div></section>
      <section id="recipes"><div className="section-heading"><h2>Your recipes</h2><button className="quiet" onClick={loadRecipes}>Refresh</button></div>{recipes.length === 0 ? <p className="muted">No saved recipes yet.</p> : <ul className="items">{recipes.map(recipe => <li key={recipe.id}><div><strong>{recipe.title}</strong><small>{recipe.servings} servings · {recipe.totalMinutes} min · revision {recipe.revision}</small></div><div className="actions"><button className="quiet" onClick={() => beginEdit(recipe)}>Edit</button><button className="danger" onClick={() => deleteRecipe(recipe)}>Delete</button></div></li>)}</ul>}</section>
    </main>
  );
}
