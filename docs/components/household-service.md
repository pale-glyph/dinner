# Household Service Specification

**Status:** Draft  
**Component type:** C# / ASP.NET Core domain service

## Purpose

Own the local household/profile context used to scope Dinner data and store preferences that influence recommendations.

## Responsibilities

- Create and update households.
- Store local user/profile records without implementing authentication.
- Store dietary preferences, allergies, dislikes, cooking skill, budget defaults, and household settings.
- Provide household context to the frontend and agent tools.
- Enforce household-level permissions within the application boundary.

## Core entities

- `Household`: name, locale, currency, timezone, defaults.
- `Profile`: display name and household-specific preferences.
- `DietaryConstraint`: type, value, severity, source, and active status.
- `HouseholdPreference`: key, typed value, and effective date.

## API

```text
GET    /api/v1/household
PATCH  /api/v1/household
GET    /api/v1/household/profiles
POST   /api/v1/household/profiles
PATCH  /api/v1/household/profiles/{profileId}
GET    /api/v1/household/preferences
PUT    /api/v1/household/preferences
```

## MCP capabilities

Read resources:

- `household://context`
- `household://preferences`
- `household://dietary-constraints`

Tools:

- `get_household_context`
- `get_household_preferences`
- `update_household_preferences`

Preference changes proposed by an agent must be presented for approval.

## Events

- `HouseholdCreated.v1`
- `HouseholdPreferencesUpdated.v1`
- `ProfileUpdated.v1`

## Acceptance criteria

- Every domain request can resolve a household context.
- Preferences are returned in a stable, typed structure suitable for agent prompts.
- Allergy constraints are clearly distinguished from ordinary dislikes.
- The service does not own authentication or external identity-provider integrations.
