import type { Recipe } from "@/types/api";

export function RecipeList({
  recipes,
  onEdit,
  onRemove,
  onRefresh,
}: {
  recipes: Recipe[];
  onEdit: (recipe: Recipe) => void;
  onRemove: (recipe: Recipe) => void;
  onRefresh: () => void;
}) {
  return (
    <section className="recipe-list">
      <div className="section-heading">
        <h2>Your recipes</h2>
        <button className="quiet" onClick={onRefresh}>
          Refresh
        </button>
      </div>
      {recipes.length === 0 ? (
        <p className="muted">No saved recipes yet.</p>
      ) : (
        <ul className="items">
          {recipes.map((recipe) => (
            <li key={recipe.id}>
              <div>
                <strong>{recipe.title}</strong>
                <small>
                  {recipe.cuisine ?? "Unspecified cuisine"} · revision {recipe.revision}
                </small>
              </div>
              <div className="actions">
                <button className="quiet" onClick={() => onEdit(recipe)}>
                  Edit
                </button>
                <button className="danger" onClick={() => onRemove(recipe)}>
                  Delete
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
