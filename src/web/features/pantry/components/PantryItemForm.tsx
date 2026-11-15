import { useState, type FormEvent } from "react";

export function PantryItemForm({
  onSubmit,
}: {
  onSubmit: (input: {
    name: string;
    quantity: number;
    unit: string;
    expiryDate: string;
  }) => Promise<boolean>;
}) {
  const [name, setName] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [unit, setUnit] = useState("item");
  const [expiryDate, setExpiryDate] = useState("");

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (
      await onSubmit({ name, quantity: Number(quantity), unit, expiryDate })
    ) {
      setName("");
      setQuantity("1");
      setExpiryDate("");
    }
  };

  return (
    <div>
      <h2>Add an item</h2>
      <form onSubmit={submit}>
        <label>
          Name
          <input
            required
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="e.g. Chickpeas"
          />
        </label>
        <div className="form-row">
          <label>
            Quantity
            <input
              required
              min="0.01"
              step="0.01"
              type="number"
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
            />
          </label>
          <label>
            Unit
            <select
              value={unit}
              onChange={(event) => setUnit(event.target.value)}
            >
              <option value="item">items</option>
              <option value="g">grams</option>
              <option value="kg">kilograms</option>
              <option value="ml">millilitres</option>
              <option value="l">litres</option>
            </select>
          </label>
        </div>
        <label>
          Expiry date <span>(optional)</span>
          <input
            type="date"
            value={expiryDate}
            onChange={(event) => setExpiryDate(event.target.value)}
          />
        </label>
        <button type="submit">Add to pantry</button>
      </form>
    </div>
  );
}
