CREATE TABLE IF NOT EXISTS households (
  id uuid PRIMARY KEY,
  name text NOT NULL,
  servings integer NOT NULL,
  dietary_preferences jsonb NOT NULL DEFAULT '[]',
  allergies jsonb NOT NULL DEFAULT '[]',
  disliked_foods jsonb NOT NULL DEFAULT '[]',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);
