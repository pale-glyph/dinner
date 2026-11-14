"use client";

import dynamic from "next/dynamic";

const App = dynamic(() => import("../../components/App").then(module => module.App), { ssr: false });

export default function Page() { return <App />; }
