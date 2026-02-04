# Architecture Documentation

This folder contains comprehensive documentation about the Discord bot's architecture, system design, and component organization.

## Documents

### [Feature Map](feature-map.md)
**Purpose**: Quick reference for understanding feature boundaries and component composition.

Maps all major features (Soundboard, VOX, Moderation, Reminders, Rat Watch, etc.) to their supporting components: Discord commands, services, UI pages, and database entities. Includes service architecture layers, cross-cutting concerns, and data retention policies.

**Best for**: Understanding what comprises each feature, finding where to add new functionality, navigating between related components.

### [UI Inventory](ui-inventory.md)
**Purpose**: Comprehensive inventory of all UI components, pages, and layouts.

Documents all Razor pages, reusable components, layouts, and their routes. Shows the complete UI structure of the admin portal.

**Best for**: Finding where a specific UI page lives, understanding page layouts and component usage.

---

## Quick Navigation

### I want to...

**Understand the overall feature structure**
→ Start with [Feature Map](feature-map.md)

**Find where a specific admin page is located**
→ Check [UI Inventory](ui-inventory.md)

**Understand how a feature works end-to-end**
→ Use Feature Map to identify components, then trace through service/command files

**Add a new feature**
→ See Feature Map's "Quick Navigation by Use Case" section

**Understand the database schema**
→ See `docs/articles/database-schema.md`

**Learn about service patterns**
→ See `docs/articles/service-architecture.md`

---

## Related Documentation

- [Service Architecture](../articles/service-architecture.md)
- [Database Schema](../articles/database-schema.md)
- [Component API](../articles/component-api.md)
- [Authorization Policies](../articles/authorization-policies.md)
- [Testing Guide](../articles/testing-guide.md)
