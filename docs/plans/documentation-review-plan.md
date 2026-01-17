# Documentation Review Plan

**Version:** v0.3.1
**Created:** 2025-12-27
**Purpose:** Comprehensive review of project documentation to identify obsolete, outdated, and missing documentation.

---

## Executive Summary

The project has grown significantly with v0.3.0 features (audit logging, scheduled messages, welcome system, SignalR real-time updates). This plan outlines a systematic review of all 50+ documentation files to ensure accuracy and completeness.

---

## Phase 1: Documentation Inventory Audit

### 1.1 Root-Level Documentation

| File | Status | Action Required |
|------|--------|-----------------|
| README.md | **Needs Update** | Add v0.3.0 features (audit logs, scheduled messages, welcome system) |
| ROADMAP.md | **Needs Update** | Move completed v0.3.0 features, update in-progress items |
| CLAUDE.md | **Updated** | Configuration options table verified and completed; command modules and precondition attributes updated (Issue #1095) |

### 1.2 Getting Started (docs/articles/)

| File | Status | Action Required |
|------|--------|-----------------|
| discord-bot-setup.md | **Review** | Verify steps still accurate |
| requirements.md | **Review** | Check .NET 8 SDK and Node.js versions |

### 1.3 Architecture & Design (docs/articles/)

| File | Status | Action Required |
|------|--------|-----------------|
| architecture-history.md | **Review** | May need v0.3.0 architecture updates |
| database-schema.md | **Needs Update** | Add new entities: AuditLog, ScheduledMessage, WelcomeConfiguration |
| repository-pattern.md | **Review** | Verify pattern documentation matches implementation |
| design-system.md | **Current** | Comprehensive, may need minor updates |
| razor-components.md | **Review** | Verify all 24 shared components documented |
| epic-2-auth-architecture-plan.md | **Potentially Obsolete** | If authentication is complete, consider archiving |

### 1.4 Security & Authentication (docs/articles/)

| File | Status | Action Required |
|------|--------|-----------------|
| identity-configuration.md | **Review** | Verify Discord OAuth flow documentation |
| authorization-policies.md | **Review** | Check if all policies documented |
| user-management.md | **Review** | Verify CRUD operations documented |

### 1.5 API & Integration (docs/articles/)

| File | Status | Action Required |
|------|--------|-----------------|
| api-endpoints.md | **Needs Update** | Add AuditLogs, ScheduledMessages, Welcome APIs |
| interactive-components.md | **Review** | Check if schedule/admin components covered |
| component-api.md | **Review** | Verify ComponentIdBuilder patterns documented |

### 1.6 Bot Features (docs/articles/)

| File | Status | Action Required |
|------|--------|-----------------|
| admin-commands.md | **Review** | Verify all admin commands documented |
| commands-page.md | **Current** | Recently updated Dec 25 |
| commands-page-design.md | **Current** | Recently updated Dec 24 |
| bot-verification.md | **Current** | Verified - correctly uses `/verify-account` command (Issue #1095) |

### 1.7 Operations & Monitoring (docs/articles/)

| File | Status | Action Required |
|------|--------|-----------------|
| environment-configuration.md | **Needs Update** | Add new configuration options |
| apm-tracing-plan.md | **Potentially Obsolete** | If tracing is implemented, archive or update |
| log-aggregation.md | **Review** | Verify Seq integration docs |
| tracing.md | **Review** | Verify OpenTelemetry setup docs |
| metrics.md | **Current** | Recently updated Dec 24 |
| grafana-dashboards-specification.md | **Current** | Recently updated Dec 24 |
| signalr-realtime.md | **Current** | Recently updated Dec 26 |

### 1.8 Other (docs/articles/)

| File | Status | Action Required |
|------|--------|-----------------|
| permissions.md | **Updated** | Precondition attributes verified and expanded to include RequireKickMembersAttribute and RequireBanMembersAttribute (Issue #1095) |
| issue-tracking-process.md | **Current** | Recently updated Dec 24 |
| versioning-strategy.md | **Review** | Verify release process |
| index.md | **Needs Update** | Update documentation index/TOC |
| toc.yml | **Needs Update** | Add new documentation entries |

---

## Phase 2: Implementation Plans Review

### 2.1 docs/plans/ - Assess for Archival

| File | Status | Action Required |
|------|--------|-----------------|
| issue-118-verification.md | **Archive Candidate** | Verification implemented |
| issue-243-signalr-infrastructure.md | **Archive Candidate** | SignalR implemented |
| issue-32-servers-list-page.md | **Archive Candidate** | Guilds page implemented |
| issue-38-data-display-components.md | **Archive Candidate** | Components implemented |
| issue-41-css-extraction.md | **Archive Candidate** | Tailwind setup complete |
| issue-4-api-layer.md | **Archive Candidate** | API layer implemented |
| issue-61-ui-components.md | **Archive Candidate** | UI components implemented |
| issue-65-authorization-policies.md | **Archive Candidate** | Authorization implemented |
| issue-76-guild-settings-edit.md | **Archive Candidate** | Guild editing implemented |

### 2.2 docs/implementation-plans/

| File | Status | Action Required |
|------|--------|-----------------|
| issue-2-data-layer.md | **Archive Candidate** | Data layer complete |
| issue-39-feedback-components.md | **Archive Candidate** | Components implemented |
| issue-66-user-management.md | **Archive Candidate** | User management complete |
| issue-83-bot-control-panel.md | **Archive Candidate** | Bot control implemented |
| issue-84-settings-page.md | **Archive Candidate** | Settings page implemented |

### 2.3 docs/designs/

| File | Status | Action Required |
|------|--------|-----------------|
| issue-118-verification-ui.md | **Archive Candidate** | Verification UI complete |

### 2.4 docs/prototypes/

| File | Status | Action Required |
|------|--------|-----------------|
| forms/implementation-plan.md | **Review** | Determine if still relevant |

### 2.5 docs/archive/

| File | Status | Action Required |
|------|--------|-----------------|
| project-setup.md | **Keep Archived** | Already archived |

---

## Phase 3: Missing Documentation Identification

### 3.1 Missing Feature Documentation (HIGH PRIORITY)

| Missing Doc | Feature | Priority | Status |
|-------------|---------|----------|--------|
| **audit-log-system.md** | Audit log fluent API, categories, retention | High | Exists |
| **scheduled-messages.md** | Schedule commands, frequencies, timezone handling | High | Exists |
| **welcome-system.md** | Welcome configuration, role assignment, message templates | High | Exists |
| **consent-privacy.md** | GDPR compliance, consent types, data handling | Medium | Exists |
| **message-logging.md** | Message log capture, retention, privacy considerations | Medium | Needs verification |
| **command-module-configuration.md** | Command module enable/disable configuration (Issue #1082) | High | **Created** |

### 3.2 Missing Operational Documentation (MEDIUM PRIORITY)

| Missing Doc | Purpose | Priority |
|-------------|---------|----------|
| **deployment-guide.md** | Production deployment steps | Medium |
| **docker-setup.md** | Container configuration (planned v0.4.0) | Low (Future) |
| **backup-restore.md** | Database backup procedures | Medium |
| **troubleshooting-guide.md** | Common issues and solutions | Medium |
| **performance-tuning.md** | Caching, query optimization | Low |

### 3.3 Missing Developer Documentation (MEDIUM PRIORITY)

| Missing Doc | Purpose | Priority |
|-------------|---------|----------|
| **testing-guide.md** | Test structure, running tests, writing tests | Medium |
| **contributing.md** | Contribution workflow (may exist at root) | Low |
| **migration-guide.md** | Upgrading between versions | Medium |

### 3.4 Missing Reference Documentation

| Missing Doc | Purpose | Priority |
|-------------|---------|----------|
| **configuration-reference.md** | Complete appsettings reference | Medium |
| **dto-reference.md** | DTO documentation (auto-generate?) | Low |

---

## Phase 4: Feature-to-Documentation Matrix

### Core Features Documentation Coverage

| Feature | Docs Exist | Docs Current | Gaps |
|---------|------------|--------------|------|
| **Discord Bot Commands** | Partial | commands-page.md current | Missing: admin commands detail |
| **Slash Command Registration** | Yes | Current | None |
| **Interactive Components** | Yes | Review needed | May need schedule/admin examples |
| **Rate Limiting** | Partial | In permissions.md | Could use expansion |
| **Audit Logging** | **NO** | N/A | **Need full documentation** |
| **Scheduled Messages** | **NO** | N/A | **Need full documentation** |
| **Welcome System** | **NO** | N/A | **Need full documentation** |
| **Message Logging** | **NO** | N/A | **Need documentation** |
| **User Consent/GDPR** | **NO** | N/A | **Need documentation** |
| **Verification System** | Yes | Review needed | May need updates |
| **User Management** | Yes | user-management.md | Review for completeness |
| **Guild Management** | Partial | In API docs | Could use dedicated page |
| **Bot Control Panel** | **NO** | N/A | Need UI documentation |
| **Settings Management** | Partial | environment-config | Need Settings page docs |
| **Dashboard/Stats** | Partial | In various files | Could consolidate |
| **SignalR Real-time** | Yes | signalr-realtime.md current | None |
| **OpenTelemetry Tracing** | Yes | tracing.md | Review accuracy |
| **Metrics Collection** | Yes | metrics.md current | None |
| **Grafana Dashboards** | Yes | Current spec | May need setup guide |
| **REST API** | Yes | api-endpoints.md | **Needs v0.3.0 endpoints** |
| **Authentication/OAuth** | Yes | identity-configuration.md | Review for accuracy |
| **Authorization** | Yes | authorization-policies.md | Review for completeness |
| **Command Logging** | Partial | In API docs | Could use dedicated page |
| **Database Schema** | Yes | database-schema.md | **Needs new entities** |

---

## Phase 5: Execution Tasks

### Task 1: Archive Completed Plans (Est. 1 hour)

1. Create `docs/archive/plans/` directory
2. Move all completed implementation plans to archive
3. Update any links in other documents

**Files to archive:**
- All files in `docs/plans/` (9 files)
- All files in `docs/implementation-plans/` (5 files)
- `docs/designs/issue-118-verification-ui.md`

### Task 2: Update Core Documentation (Est. 3-4 hours)

1. **README.md** - Add v0.3.0 features section
2. **ROADMAP.md** - Update completed/in-progress features
3. **database-schema.md** - Add AuditLog, ScheduledMessage, WelcomeConfiguration entities
4. **api-endpoints.md** - Add new controller endpoints
5. **environment-configuration.md** - Add new configuration options
6. **toc.yml** - Update table of contents

### Task 3: Create Missing Feature Documentation (Est. 4-5 hours)

Create new documentation files:

1. **audit-log-system.md** (High Priority)
   - Fluent builder API usage
   - Audit log categories and actions
   - Retention configuration
   - Dashboard widget
   - API endpoints

2. **scheduled-messages.md** (High Priority)
   - Slash command reference
   - Schedule frequencies
   - Timezone handling
   - Admin UI pages
   - Background execution service

3. **welcome-system.md** (High Priority)
   - Welcome configuration
   - Message templates
   - Role assignment
   - Slash commands
   - Admin UI configuration

4. **consent-privacy.md** (Medium Priority)
   - GDPR compliance approach
   - Consent types
   - User data handling
   - /consent and /privacy commands

5. **message-logging.md** (Medium Priority)
   - Message capture configuration
   - Retention policies
   - Privacy considerations
   - Admin UI pages

### Task 4: Update Existing Documentation (Est. 2-3 hours)

Review and update as needed:
- authorization-policies.md - Verify all policies
- interactive-components.md - Add schedule/admin component examples
- razor-components.md - Verify all 24 components documented
- permissions.md - Expand precondition documentation

### Task 5: Create Operational Documentation (Est. 2-3 hours)

1. **troubleshooting-guide.md** - Expand common issues section
2. **testing-guide.md** - Document test structure and practices

### Task 6: Final Review (Est. 1 hour)

1. Verify all internal links work
2. Run DocFX build and check for warnings
3. Review table of contents completeness
4. Update CLAUDE.md if needed

---

## Phase 6: Documentation Standards Checklist

When creating or updating documentation, ensure:

- [ ] File uses consistent heading hierarchy
- [ ] Code examples are accurate and tested
- [ ] Configuration examples use placeholder values (not real secrets)
- [ ] Internal links use relative paths
- [ ] Tables are properly formatted
- [ ] Version references are accurate (currently v0.3.1)
- [ ] Feature status is clear (implemented vs planned)

---

## Summary

### Documents to Delete/Archive: 15+
- 9 plan files in `docs/plans/`
- 5 plan files in `docs/implementation-plans/`
- 1 design file
- Potentially: `epic-2-auth-architecture-plan.md`, `apm-tracing-plan.md`

### Documents to Update: 8+
- README.md
- ROADMAP.md
- database-schema.md
- api-endpoints.md
- environment-configuration.md
- toc.yml
- index.md
- CLAUDE.md (configuration options table)

### Documents to Create: 7+
- audit-log-system.md (HIGH)
- scheduled-messages.md (HIGH)
- welcome-system.md (HIGH)
- consent-privacy.md (MEDIUM)
- message-logging.md (MEDIUM)
- troubleshooting-guide.md (MEDIUM)
- testing-guide.md (MEDIUM)

---

## Next Steps

1. Review and approve this plan
2. Prioritize tasks based on team capacity
3. Create GitHub issues for tracking (optional)
4. Execute tasks in priority order
5. Perform final documentation review
