import type { PantryItem } from "@/types/api";

type PantryItemListProps = {
  items: PantryItem[];
  onAdjust: (item: PantryItem, kind: "consume" | "restore") => void;
  onRemove: (id: string) => void;
};

export function PantryItemList({
  items,
  onAdjust,
  onRemove,
}: PantryItemListProps) {
  if (items.length === 0) return <p className="muted">Nothing saved yet.</p>;

  return (
    <ul className="items">
      {items.map((item) => (
        <li key={item.id}>
          <div>
            <strong>{item.name}</strong>
            <small>
              {item.quantity} {item.unit}
              {item.location ? ` · ${item.location}` : ""}
              {item.expiryDate ? ` · expires ${item.expiryDate}` : ""}
            </small>
          </div>
          <div className="actions">
            <button className="quiet" onClick={() => onAdjust(item, "consume")}>
              Use 1
            </button>
            <button className="quiet" onClick={() => onAdjust(item, "restore")}>
              Restore
            </button>
            <button
              className="danger"
              aria-label={`Remove ${item.name}`}
              onClick={() => onRemove(item.id)}
            >
              Remove
            </button>
          </div>
        </li>
      ))}
    </ul>
  );
}
