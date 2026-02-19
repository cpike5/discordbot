# Architecture Documentation

Comprehensive reference for the Discord bot's system architecture, design patterns, and component organization. All documents reflect the current codebase at **v1.0.1-dev**.

---

## Documents

### [System Overview](system-overview.md)

High-level architecture, layer boundaries, data flows, and deployment topology. Start here to understand how the three-layer clean architecture (Core, Infrastructure, Bot) fits together, how Discord events flow through the system, and how the application is deployed with Docker.

**Best for**: New contributors, understanding the big picture, tracing a data flow end-to-end.

---

### [Data Model](data-model.md)

Entity relationships, database schema reference, and cascade behavior. Documents all EF Core entities with their fields, types, and relationships. Includes an ERD diagram and notes on soft deletes and retention policies.

**Best for**: Writing database queries, designing new entities, understanding foreign key relationships.

---

### [Service Catalog](service-catalog.md)

Quick reference catalog of every service in the system, organized by domain area. Each entry includes the service name, its location (Core interface vs. Bot/Infrastructure implementation), and a one-line description of its purpose.

**Best for**: Discovering existing services before building something new, finding the right service to inject, understanding service boundaries.

---

### [Feature Map](feature-map.md)

Maps each major feature (Soundboard, VOX, Moderation, Reminders, Rat Watch, AI Assistant, etc.) to its supporting components: Discord commands, services, UI pages, and database entities.

**Best for**: Understanding what comprises a specific feature, finding where to add new functionality within a feature, navigating between related command modules and services.

---

### [Patterns](patterns.md)

Recurring implementation patterns and conventions used throughout the project. Covers DI registration, configuration (Options pattern), Discord command structure, Razor Pages, data access (repository pattern), authorization, audit logging, error handling, background service base classes, memory reporting, and per-guild locking.

**Best for**: Implementing a new feature consistently, reviewing how a cross-cutting concern should be handled, onboarding to coding conventions.

---

### [UI Inventory](ui-inventory.md)

Complete inventory of all Razor pages, reusable UI components, layouts, and their routes in the admin portal.

**Best for**: Finding where a specific UI page lives, understanding page layouts and which components they use, adding new pages to the right location.

---

## Quick Navigation

### I want to...

**Understand how the system works overall**
→ Start with [System Overview](system-overview.md)

**Understand the database schema and entity relationships**
→ See [Data Model](data-model.md)

**Find an existing service to use or extend**
→ Search [Service Catalog](service-catalog.md)

**Understand what components make up a specific feature**
→ See [Feature Map](feature-map.md)

**Write a new background service**
→ See [Patterns - MonitoredBackgroundService](patterns.md#monitoredbackgroundservice)

**Write a new slash command**
→ See [Patterns - Discord Commands](patterns.md#discord-commands)

**Write a new Razor Page**
→ See [Patterns - Razor Pages](patterns.md#razor-pages)

**Register new services in DI**
→ See [Patterns - DI Registration](patterns.md#di-registration)

**Add a new database entity**
→ See [Patterns - Data Access](patterns.md#data-access), then [Data Model](data-model.md)

**Find where a specific admin page is located**
→ See [UI Inventory](ui-inventory.md)

**Understand how audio/voice flows through the system**
→ See [System Overview - Data Flows](system-overview.md), then [Service Catalog - Audio & Voice Services](service-catalog.md#audio--voice-services)

**Understand role-based authorization**
→ See [Patterns - Authorization](patterns.md#authorization) and [docs/articles/authorization-policies.md](../articles/authorization-policies.md)

---

## Related Documentation

The `docs/articles/` folder contains feature-specific guides that complement these architecture docs:

- [Component API](../articles/component-api.md) - Razor UI component library
- [Authorization Policies](../articles/authorization-policies.md) - Role hierarchy and policies
- [Audit Log System](../articles/audit-log-system.md) - Audit logging guide
- [Database Schema](../articles/database-schema.md) - Full schema reference
- [Testing Guide](../articles/testing-guide.md) - Testing patterns and fixtures
- [Docker Deployment](../articles/docker-deployment.md) - Docker and Compose deployment
