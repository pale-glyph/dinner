CREATE TABLE IF NOT EXISTS pantry_items (
  id uuid PRIMARY KEY, household_id uuid NOT NULL, name text NOT NULL,
  quantity numeric NOT NULL CHECK (quantity >= 0), unit text NOT NULL,
  location text NULL, expiry_date date NULL, low_stock_threshold numeric NULL,
  product_code text NULL, food_product_id text NULL,
  created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS pantry_adjustments (
  id uuid PRIMARY KEY, pantry_item_id uuid NOT NULL REFERENCES pantry_items(id) ON DELETE CASCADE,
  kind text NOT NULL, quantity numeric NOT NULL, unit text NOT NULL, note text NULL,
  occurred_at timestamptz NOT NULL DEFAULT now()
);
