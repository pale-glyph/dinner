# Notification Service Specification

**Status:** Deferred / Draft  
**Component type:** C# / ASP.NET Core supporting service

## Purpose

Deliver user-facing notifications for important Dinner events after the core pantry, shopping, meal-planning, and agent workflows are stable.

## Initial scope

- In-app notifications.
- Optional email notifications.
- Notifications for expiring pantry items, completed agent runs, meal-plan reminders, and shopping-list reminders.

Push notifications and external messaging channels are out of scope until there is a confirmed product requirement.

## Responsibilities

- Subscribe to relevant integration events.
- Apply household/profile notification preferences.
- Create notification records and delivery attempts.
- Deduplicate repeated events.
- Expose read and dismiss operations to the frontend.

## API

```text
GET   /api/v1/notifications
POST  /api/v1/notifications/{notificationId}/read
POST  /api/v1/notifications/{notificationId}/dismiss
GET   /api/v1/notifications/preferences
PUT   /api/v1/notifications/preferences
```

## Events consumed

- `PantryItemExpiring.v1`
- `MealPlanCreated.v1`
- `AgentRunCompleted.v1`
- `AgentRunFailed.v1`
- `ShoppingListReminderDue.v1`

## Business rules

- Notifications must be scoped to the correct household/profile.
- Repeated events should not create noisy duplicate notifications.
- Delivery failure must not change the source domain state.
- Notification text may be AI-assisted, but event type and destination must be deterministic.

## Acceptance criteria

- A user can see, read, and dismiss in-app notifications.
- Notification preferences can disable non-critical categories.
- Failed delivery attempts are observable and retryable.
