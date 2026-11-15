"use client";

import { useState } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { UserButton, useUser } from "@clerk/nextjs";
import { useHousehold } from "./HouseholdProvider";

const navigation = [
  { to: "/pantry", label: "Pantry", icon: "▦" },
  { to: "/assistant", label: "Recipe assistant", icon: "✦" },
  { to: "/recipes", label: "My recipes", icon: "▤" }
];

export function AppShell() {
  const { user } = useUser();
  const { household } = useHousehold();
  const [isOpen, setIsOpen] = useState(false);
  const location = useLocation();
  const currentPage = navigation.find(item => item.to === location.pathname)?.label ?? "Dinner";
  const closeMenu = () => setIsOpen(false);
  return <div className="app-shell">
    <aside className={`app-drawer ${isOpen ? "app-drawer-open" : ""}`} aria-label="Application navigation">
      <div className="brand-row"><div className="brand-mark" aria-hidden="true">d</div><span className="brand-name">dinner</span><button className="icon-button close-button" onClick={closeMenu} aria-label="Close navigation">×</button></div>
      <div className="drawer-label">Workspace</div>
      <nav className="app-nav">{navigation.map(item => <NavLink key={item.to} to={item.to} onClick={closeMenu} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}><span className="nav-icon" aria-hidden="true">{item.icon}</span><span>{item.label}</span></NavLink>)}</nav>
      <div className="drawer-footer"><div className="account-avatar">{user?.firstName?.slice(0, 1).toUpperCase() ?? "D"}</div><div><strong>{household?.name ?? "My household"}</strong><span>{household?.role === "owner" ? "Household owner" : "Household member"}</span></div></div>
    </aside>
    {isOpen && <button className="drawer-scrim" aria-label="Close navigation" onClick={closeMenu} />}
    <div className="app-main"><header className="top-app-bar"><button className="icon-button menu-button" onClick={() => setIsOpen(true)} aria-label="Open navigation"><span /><span /><span /></button><div className="top-bar-title">{currentPage}</div><div className="account-menu"><UserButton /></div></header><main className="page-content"><Outlet /></main></div>
  </div>;
}
