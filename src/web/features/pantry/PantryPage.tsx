import { useHousehold } from "@/components/providers/HouseholdProvider";
import { PantryItemForm } from "@/features/pantry/components/PantryItemForm";
import { PantryItemList } from "@/features/pantry/components/PantryItemList";
import { usePantry } from "@/features/pantry/hooks/usePantry";

export function PantryPage() {
  const { household, loading } = useHousehold();
  const { items, status, load, addItem, adjustItem, removeItem } = usePantry(
    household?.id,
  );

  if (loading) return <p className="status">Loading your household…</p>;
  if (!household)
    return <p className="status">Your household could not be loaded.</p>;

  return (
    <section className="pantry-grid">
      <PantryItemForm onSubmit={addItem} />
      <div>
        <div className="section-heading">
          <h2>On hand</h2>
          <button className="quiet" onClick={load}>
            Refresh
          </button>
        </div>
        {status && (
          <p role="status" className="status">
            {status}
          </p>
        )}
        {!status && (
          <PantryItemList
            items={items}
            onAdjust={adjustItem}
            onRemove={removeItem}
          />
        )}
      </div>
    </section>
  );
}
