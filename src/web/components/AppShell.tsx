import { NavLink, Outlet } from "react-router-dom";

export function AppShell() {
  return <main>
    <p className="eyebrow">Dinner · your kitchen</p>
    <h1>Dinner</h1>
    <p className="intro">Plan dinner from what you have—or start from a fresh idea.</p>
    <nav aria-label="Primary navigation">
      <NavLink to="/pantry">Pantry</NavLink>
      <NavLink to="/assistant">Recipe chat</NavLink>
      <NavLink to="/recipes">Recipes</NavLink>
    </nav>
    <Outlet />
  </main>;
}
