import { NavLink, Outlet } from "react-router-dom";

export function AppShell() {
  return <main className="min-h-screen bg-[radial-gradient(circle_at_top_right,_rgba(34,211,238,.12),_transparent_28rem)]">
    <div className="mx-auto grid max-w-7xl gap-6 px-4 py-5 sm:px-6 lg:min-h-screen lg:grid-cols-[15rem_minmax(0,1fr)] lg:px-8 lg:py-8">
      <aside className="rounded-2xl border border-slate-800 bg-slate-900/80 p-5 shadow-2xl shadow-black/20 backdrop-blur lg:h-fit lg:sticky lg:top-8">
        <p className="eyebrow">Dinner</p><h1 className="text-3xl font-bold tracking-tight text-white">Your kitchen</h1>
        <p className="mt-3 text-sm leading-6 text-slate-400">Dinner ideas, pantry context, and approval-controlled recipes.</p>
        <nav aria-label="Primary navigation" className="mt-6 grid gap-2">
          <NavLink className="rounded-xl px-3 py-2 text-sm font-medium text-slate-400 transition hover:bg-slate-800 hover:text-white" to="/pantry">Pantry</NavLink>
          <NavLink className="rounded-xl px-3 py-2 text-sm font-medium text-slate-400 transition hover:bg-slate-800 hover:text-white" to="/assistant">Recipe chat</NavLink>
          <NavLink className="rounded-xl px-3 py-2 text-sm font-medium text-slate-400 transition hover:bg-slate-800 hover:text-white" to="/recipes">Recipes</NavLink>
        </nav>
      </aside>
      <div className="space-y-6"><Outlet /></div>
    </div>
  </main>;
}
