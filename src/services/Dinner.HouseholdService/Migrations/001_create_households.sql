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

CREATE TABLE IF NOT EXISTS accounts (
  id uuid PRIMARY KEY,
  clerk_user_id text NOT NULL UNIQUE,
  email text NULL,
  display_name text NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS household_members (
  household_id uuid NOT NULL REFERENCES households(id) ON DELETE CASCADE,
  account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
  role text NOT NULL CHECK (role IN ('owner', 'member')),
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (household_id, account_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_household_members_one_owner
  ON household_members(household_id) WHERE role = 'owner';

CREATE TABLE IF NOT EXISTS household_invites (
  token text PRIMARY KEY,
  household_id uuid NOT NULL REFERENCES households(id) ON DELETE CASCADE,
  created_by_account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  revoked_at timestamptz NULL
);
