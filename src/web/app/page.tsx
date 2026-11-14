const routes = ["Pantry", "Shopping", "Plan", "Recipes", "Assistant", "Activity"];

export default function Home() {
  return (
    <main>
      <p className="eyebrow">Dinner · MVP skeleton</p>
      <h1>Make dinner easier.</h1>
      <p className="intro">The application shell is ready for the household and pantry vertical slice.</p>
      <nav aria-label="Primary navigation">
        {routes.map((route) => <a href="#" key={route}>{route}</a>)}
      </nav>
      <section>
        <h2>Getting started</h2>
        <p>Start a service and connect this shell to its versioned API when the first vertical slice is implemented.</p>
      </section>
    </main>
  );
}
