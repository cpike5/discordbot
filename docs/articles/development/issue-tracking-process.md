# Issue Tracking Process

This document defines the standards for tracking work using GitHub Issues in this repository.

## Issue Hierarchy

We use a four-level hierarchy to organize work:

| Type | Label | Purpose | Contains |
|------|-------|---------|----------|
| **Epic** | `epic` | Major version/release initiative | Multiple Features |
| **Feature** | `feature` | Single feature stream | Multiple Tasks |
| **Task** | `task` | Specific implementation work | N/A |
| **Bug** | `bug` | Defect fix | N/A |

### Epic

Epics represent large initiatives that span multiple features, typically aligned with a major or minor version release.

**Characteristics:**
- High-level business or technical goal
- Spans multiple features
- May take multiple sprints/iterations to complete
- Tracks overall progress of a release or major initiative

**Example:** "v1.0 Release - Production Ready Bot"

### Feature

Features represent a single coherent piece of functionality that delivers user value.

**Characteristics:**
- Implements one logical capability
- Can be broken down into multiple tasks
- Should have clear acceptance criteria
- Links to parent Epic (if applicable)

**Example:** "Discord OAuth Integration for Admin UI"

### Task

Tasks are the atomic units of work that developers pick up and complete.

**Characteristics:**
- Specific, actionable implementation work
- Should be completable in a reasonable timeframe
- Links to parent Feature (if applicable)
- May include: implementation, prototyping, research, documentation

**Example:** "Create OAuth callback handler for Discord login"

### Bug

Bugs track defects that need to be fixed.

**Characteristics:**
- Describes incorrect behavior
- Includes reproduction steps
- Specifies expected vs actual behavior
- May link to related Feature if discovered during development

**Example:** "Login button not visible on mobile devices"

## Labeling Standards

### Required Labels

Every issue **must** have:
1. **Type label** - One of: `epic`, `feature`, `task`, `bug`
2. **Priority label** - One of: `priority:critical`, `priority:high`, `priority:medium`, `priority:low`

### Optional Labels

Apply these when relevant:

| Category | Labels | When to Use |
|----------|--------|-------------|
| **Component** | `component:bot`, `component:api`, `component:data`, `component:ui`, `component:infra` | To indicate which part of the system is affected |
| **Phase** | `phase:1` through `phase:6` | For roadmap alignment during initial development |
| **Special** | `observability`, `logging`, `performance`, `security`, `metrics`, `tracing`, `privacy` | For cross-cutting concerns |

### Priority Definitions

| Priority | Definition | Response Time |
|----------|------------|---------------|
| `priority:critical` | System down, data loss, security vulnerability | Immediate |
| `priority:high` | Major functionality broken, blocking issue | Next available cycle |
| `priority:medium` | Important but not urgent, normal queue | Planned iteration |
| `priority:low` | Nice to have, minor improvements | When time permits |

## Issue Linking

Use GitHub's linking features to maintain hierarchy:

### Parent-Child Relationships

In the issue body, reference parent issues:

```markdown
**Parent Epic:** #42
**Parent Feature:** #87
```

### Task Lists in Features

Features should list their tasks as a checklist:

```markdown
## Tasks
- [ ] #101 - Create database schema
- [ ] #102 - Implement repository layer
- [ ] #103 - Add API endpoints
- [ ] #104 - Write unit tests
```

### Closing Issues via Commits

Reference issues in commit messages to auto-close:

```
feat: Add OAuth callback handler (closes #101)
```

## Branch Naming Convention

Branches should reference the issue number:

```
feature/{issue-number}-{brief-description}
```

**Examples:**
- `feature/101-oauth-callback-handler`
- `feature/87-discord-oauth-integration`
- `bugfix/156-mobile-login-button`

## Workflow

### Creating New Work

1. **Epic** - Created when planning a new major initiative or release
2. **Feature** - Created to break down an Epic into deliverable features
3. **Task** - Created when a Feature is ready for implementation
4. **Bug** - Created when a defect is discovered

### Issue Lifecycle

```
Open → In Progress → In Review → Closed
```

1. **Open** - Issue created, not yet started
2. **In Progress** - Work actively being done (assign yourself)
3. **In Review** - PR submitted, awaiting review
4. **Closed** - Work complete and merged

### Before Creating an Issue

1. Search existing issues to avoid duplicates
2. Check if the work fits under an existing Feature or Epic
3. Use the appropriate issue template
4. Apply required labels (type + priority)

## Issue Templates

GitHub issue templates are available for each issue type:

- **Epic** - For major initiatives spanning multiple features
- **Feature** - For new functionality with acceptance criteria
- **Task** - For specific implementation work
- **Bug** - For defect reports with reproduction steps

Templates are located in `.github/ISSUE_TEMPLATE/` and will appear when creating a new issue.

## Best Practices

1. **Keep issues focused** - One issue = one piece of work
2. **Write clear titles** - Should describe the outcome, not the action
3. **Link related issues** - Maintain traceability between Epic → Feature → Task
4. **Update status** - Assign yourself when starting, close when done
5. **Reference in commits** - Use `#issue-number` in commit messages
6. **Don't leave issues stale** - Close or update issues that are no longer relevant
