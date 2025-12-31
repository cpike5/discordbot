# Documentation Articles

This section contains conceptual documentation and developer guides for the Discord Bot Management System.

## Getting Started
- [Discord Bot Setup](discord-bot-setup.md) - How to obtain a bot token and configure your Discord bot
- [Requirements](requirements.md) - Technology stack and architecture requirements
- [Troubleshooting Guide](troubleshooting-guide.md) - Common issues and solutions

## Reference
- [Architecture History](architecture-history.md) - Original implementation plan (historical)

## Architecture & Design
- [Database Schema](database-schema.md) - Entity definitions and relationships
- [Repository Pattern](repository-pattern.md) - Data access implementation
- [Design System](design-system.md) - UI design tokens and components
- [Razor Components](razor-components.md) - Reusable Razor components specification
- [Identity Configuration](identity-configuration.md) - ASP.NET Identity setup and Discord OAuth integration

## Security & Authentication
- [Identity Configuration](identity-configuration.md) - ASP.NET Identity setup, Discord OAuth, and security settings
- [Authorization Policies](authorization-policies.md) - Role-based authorization policies and implementation
- [User Management Guide](user-management.md) - Admin user management feature documentation
- [Consent & Privacy](consent-privacy.md) - GDPR consent management and user privacy controls

## API & Integration
- [REST API Endpoints](api-endpoints.md) - Complete REST API reference
- [Interactive Components](interactive-components.md) - Discord component patterns

## Bot Features
- [Admin Commands](admin-commands.md) - Slash command reference
- [Commands Page](commands-page.md) - Admin UI for viewing registered commands and metadata
- [Settings Page](settings-page.md) - Admin UI for configuring bot settings
- [Bot Account Verification](bot-verification.md) - Discord bot account verification feature
- [Permissions](permissions.md) - Permission system documentation
- [Scheduled Messages](scheduled-messages.md) - Automated message scheduling with cron expressions
- [Welcome System](welcome-system.md) - Automated welcome messages for new members
- [Rat Watch](rat-watch.md) - Accountability and task tracking feature

## Operations & Monitoring
- [Audit Log System](audit-log-system.md) - Comprehensive audit logging, retention, and compliance tracking
- [Message Logging](message-logging.md) - Discord message logging with consent-based collection
- [Environment Configuration](environment-configuration.md) - Environment-specific settings (Dev, Staging, Production)
- [Distributed Tracing](tracing.md) - OpenTelemetry distributed tracing
- [Centralized Logging (Seq)](log-aggregation.md) - Log aggregation with Seq

## Development
- [Testing Guide](testing-guide.md) - Unit testing patterns and best practices
