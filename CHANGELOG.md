# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Theme Switching System** - Users can now personalize their admin UI experience with selectable themes
  - New **Purple Dusk** light theme with warm beige backgrounds and purple/pink accent colors
  - User profile page (`/Account/Profile`) with theme selector
  - Admin Settings > Appearance tab for SuperAdmins to configure default theme
  - Theme preferences persist across sessions via database (authenticated) and cookies (anonymous)
  - Theme API endpoints for programmatic theme management (`/api/theme/*`)
  - CSS variable-based theming architecture for easy future theme additions
  - Full WCAG 2.1 AA contrast compliance across all theme combinations

### Changed

- Updated `design-system.md` with comprehensive theming documentation including:
  - Complete Purple Dusk color palette with hex and HSL values
  - Theme architecture explanation
  - CSS variable usage guidelines
  - Instructions for adding new themes

### Documentation

- Added Theme API section to `api-endpoints.md` with full endpoint documentation
- Enhanced CSS comments in `site.css` for theme-related custom properties
- Updated `CLAUDE.md` with theming reference in Key Documentation table

---

## [0.7.6] - 2026-01-02

### Added

- Initial release features (see project documentation for details)

[Unreleased]: https://github.com/cpike5/discordbot/compare/v0.7.6...HEAD
[0.7.6]: https://github.com/cpike5/discordbot/releases/tag/v0.7.6
