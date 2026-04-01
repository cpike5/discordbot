# Versioning Strategy

**Last Updated:** 2025-12-24
**Related Documentation:** [Environment Configuration](environment-configuration.md)
**GitHub Workflows:** `.github/workflows/release.yml`, `.github/workflows/ci.yml`

---

## Overview

The Discord Bot Management System follows **Semantic Versioning 2.0.0** (SemVer) to communicate changes and maintain compatibility across releases. All version metadata is centralized in `Directory.Build.props` at the solution root, ensuring consistent versioning across all assemblies and build artifacts.

### Key Features

- **Semantic Versioning 2.0.0** for predictable release semantics
- **Centralized version source** in `Directory.Build.props`
- **Git commit hash traceability** in build artifacts
- **Automated releases** via GitHub Actions
- **Pre-release versioning** for 0.x.y development cycle

### Quick Reference

| Action | Command/File | Purpose |
|--------|--------------|---------|
| View current version | Check `Directory.Build.props` | Source of truth for all version metadata |
| Display version in UI | `IVersionService.GetVersion()` | Returns formatted version string (e.g., "v0.2.0") |
| Create a release | `git tag v0.2.0 && git push origin v0.2.0` | Triggers automated release workflow |
| Version properties | `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion` | MSBuild properties in Directory.Build.props |

---

## Semantic Versioning 2.0.0

The project strictly adheres to the [Semantic Versioning 2.0.0](https://semver.org/) specification. Version numbers use the format **MAJOR.MINOR.PATCH**, where each component has specific semantics:

### Version Components

| Component | Increment When | Examples | Impact |
|-----------|----------------|----------|--------|
| **MAJOR** | Breaking changes to bot commands, API endpoints, or database schema | v0.x.y → v1.0.0 (stable release)<br/>v1.2.3 → v2.0.0 (breaking change) | May require migration scripts, configuration changes, or client updates |
| **MINOR** | New features, commands, or non-breaking enhancements | v0.1.0 → v0.2.0 (new feature)<br/>v1.0.0 → v1.1.0 (new API endpoint) | Backward compatible, safe to upgrade |
| **PATCH** | Bug fixes and minor improvements | v0.2.0 → v0.2.1 (bug fix)<br/>v1.1.0 → v1.1.1 (performance fix) | Backward compatible, recommended to upgrade |

### Breaking Change Examples

**MAJOR version bumps are required for:**

1. **Bot Command Changes**
   - Renaming or removing slash commands
   - Changing command parameters (adding required params, removing params)
   - Changing command behavior that users depend on

2. **API Endpoint Changes**
   - Modifying REST API request/response schemas
   - Removing or renaming API endpoints
   - Changing authentication requirements

3. **Database Schema Changes**
   - Removing or renaming tables or columns
   - Changing column data types in incompatible ways
   - Adding non-nullable columns without defaults

4. **Configuration Changes**
   - Removing or renaming configuration keys
   - Changing configuration value formats or validation rules

### Pre-Release Versioning (0.x.y)

The project is currently in **pre-release** status (version 0.x.y), indicating that the public API is not yet stable. During the 0.x.y phase:

- **Current Version:** 0.2.0
- **Stable Release Target:** v1.0.0 (planned after core features are complete and stable)
- **Breaking Changes Allowed:** MINOR version bumps may include breaking changes (0.1.0 → 0.2.0)
- **API Stability:** The public API is subject to change without MAJOR version increment

**When v1.0.0 is released:**
- Public API is considered stable
- MAJOR.MINOR.PATCH semantics strictly enforced
- Breaking changes require MAJOR version bump

---

## Version Source of Truth

All version metadata is centralized in `Directory.Build.props` at the solution root. This MSBuild file is automatically imported by all projects in the solution, ensuring consistent versioning across assemblies.

### Directory.Build.props Structure

**Location:** `C:\Users\cpike\workspace\discordbot\Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <!-- Primary version number (SemVer format) -->
    <Version>0.2.0</Version>

    <!-- Assembly version (4-part .NET format) -->
    <AssemblyVersion>0.2.0.0</AssemblyVersion>

    <!-- File version (displayed in Windows file properties) -->
    <FileVersion>0.2.0.0</FileVersion>

    <!-- Informational version (can include git commit hash) -->
    <!-- Overridden during CI builds to include commit hash -->
    <InformationalVersion>$(Version)</InformationalVersion>

    <Authors>cpike5</Authors>
    <Company>cpike5</Company>
    <Product>DiscordBot</Product>
    <Copyright>Copyright © 2024-2025</Copyright>
    <RepositoryUrl>https://github.com/cpike5/discordbot</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>
</Project>
```

### Version Properties Explained

| Property | Purpose | Format | Example |
|----------|---------|--------|---------|
| `Version` | NuGet package version and primary version identifier | `MAJOR.MINOR.PATCH[-prerelease]` | `0.2.0` |
| `AssemblyVersion` | .NET assembly version for strong naming | `MAJOR.MINOR.PATCH.REVISION` | `0.2.0.0` |
| `FileVersion` | Windows file version metadata | `MAJOR.MINOR.PATCH.REVISION` | `0.2.0.0` |
| `InformationalVersion` | Human-readable version with metadata | `MAJOR.MINOR.PATCH[+metadata]` | `0.2.0+abc1234` |

### CI Build Version Augmentation

During CI builds (GitHub Actions), the `InformationalVersion` property is **overridden** to include the git commit hash for traceability:

**Local Build:**
```
InformationalVersion: 0.2.0
```

**CI Build (Release):**
```
InformationalVersion: 0.2.0+abc1234
```

The commit hash suffix (`+abc1234`) follows SemVer 2.0.0 build metadata conventions and does not affect version precedence.

---

## Build Artifacts and Version Metadata

All compiled assemblies carry consistent version metadata embedded by MSBuild. This metadata is accessible via .NET reflection APIs and displayed in the admin UI.

### Assembly Metadata Attributes

Each compiled assembly includes the following version attributes:

```csharp
// Example: DiscordBot.Bot.dll assembly attributes
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
[assembly: AssemblyInformationalVersion("0.2.0+abc1234")]
[assembly: AssemblyProduct("DiscordBot")]
[assembly: AssemblyCopyright("Copyright © 2024-2025")]
```

### Version Display in Admin UI

The `VersionService` retrieves and formats version information for display in the admin UI sidebar and Bot Control page.

**Implementation:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\VersionService.cs`

```csharp
public class VersionService : IVersionService
{
    private readonly string _version;

    public VersionService()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly?.GetName().Version?.ToString()
            ?? "0.0.0";

        // Strip commit hash suffix (e.g., "+abc123") for clean UI display
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        _version = $"v{version}";
    }

    public string GetVersion() => _version;
}
```

**Usage in Razor Pages:**

```cshtml
@inject DiscordBot.Core.Interfaces.IVersionService VersionService

<span class="text-xs text-text-tertiary">@VersionService.GetVersion()</span>
```

**Displayed Version:** `v0.2.0` (commit hash stripped for clean presentation)

---

## Release Process

Releases are fully automated via GitHub Actions. Pushing a version tag triggers the release workflow, which builds, tests, packages, and publishes the release.

### Automated Release Workflow

**Workflow File:** `.github/workflows/release.yml`

**Trigger:** Push of version tags matching pattern `v*` (e.g., `v0.2.0`, `v1.0.0`)

**Workflow Steps:**

1. **Checkout Code** with full git history for changelog generation
2. **Extract Version** from tag (removes `v` prefix: `v1.2.3` → `1.2.3`)
3. **Get Commit SHA** for InformationalVersion metadata
4. **Setup .NET 8 SDK** and **Node.js 20**
5. **Restore Dependencies** (NuGet and npm)
6. **Build Solution** with version and commit hash:
   ```bash
   dotnet build --configuration Release \
     /p:Version=0.2.0 \
     /p:InformationalVersion="0.2.0+abc1234"
   ```
7. **Run Tests** to validate build integrity
8. **Publish Application** to `./publish/bot/` directory
9. **Create Release Archive** (`discordbot-0.2.0.zip`)
10. **Generate SHA256 Checksums** for artifact integrity
11. **Create GitHub Release** with auto-generated release notes
12. **Upload Release Assets** (ZIP archive and checksums)
13. **Upload Build Artifacts** (retained for 90 days)

### Release Artifact Structure

Each release produces the following artifacts:

| Artifact | Description | Retention |
|----------|-------------|-----------|
| `discordbot-X.Y.Z.zip` | Self-contained deployment package with all binaries and assets | Permanent (GitHub Release) |
| `checksums.txt` | SHA256 checksums for integrity verification | Permanent (GitHub Release) |
| Build artifacts | Uncompressed binaries for debugging | 90 days (GitHub Actions) |

### Pre-Release Detection

The workflow automatically detects pre-release versions (containing hyphens) and marks them accordingly:

**Example Pre-Release Versions:**
- `v0.2.0-alpha`
- `v1.0.0-beta.1`
- `v2.0.0-rc.1`

**GitHub Release Metadata:**
```yaml
prerelease: ${{ contains(steps.extract_version.outputs.version, '-') }}
```

---

## How to Create a Release

Follow these steps to create and publish a new release:

### Step 1: Update Version in Directory.Build.props

Edit `Directory.Build.props` and update the `Version` property:

```xml
<PropertyGroup>
  <Version>0.3.0</Version>
  <AssemblyVersion>0.3.0.0</AssemblyVersion>
  <FileVersion>0.3.0.0</FileVersion>
  <InformationalVersion>$(Version)</InformationalVersion>
</PropertyGroup>
```

**Version Selection Guidelines:**
- **PATCH:** Bug fixes only → increment PATCH (0.2.0 → 0.2.1)
- **MINOR:** New features, backward compatible → increment MINOR (0.2.0 → 0.3.0)
- **MAJOR:** Breaking changes (or v1.0.0 stable) → increment MAJOR (0.9.0 → 1.0.0)

### Step 2: Commit Version Bump

Commit the version change with a conventional commit message:

```bash
git add Directory.Build.props
git commit -m "chore: bump version to 0.3.0"
```

**Recommended Commit Message Format:**
```
chore: bump version to X.Y.Z
```

### Step 3: Create and Push Version Tag

Create an annotated git tag matching the version number (with `v` prefix):

```bash
# Create annotated tag
git tag -a v0.3.0 -m "Release v0.3.0"

# Push tag to GitHub (triggers release workflow)
git push origin v0.3.0
```

**Tag Naming Convention:**
- Format: `v{MAJOR}.{MINOR}.{PATCH}[-prerelease]`
- Examples: `v0.3.0`, `v1.0.0`, `v1.1.0-beta.1`
- **Always include the `v` prefix** to match workflow trigger pattern

### Step 4: Monitor Release Workflow

1. Navigate to **GitHub Actions** tab in the repository
2. Locate the **Release** workflow run triggered by the tag push
3. Monitor workflow progress (build, test, publish)
4. Verify all steps complete successfully

**Workflow Duration:** Typically 5-10 minutes

### Step 5: Verify GitHub Release

Once the workflow completes:

1. Navigate to **Releases** section in GitHub repository
2. Verify the new release appears with correct version number
3. Download and verify release artifacts:
   - `discordbot-X.Y.Z.zip` (deployment package)
   - `checksums.txt` (integrity verification)
4. Review auto-generated release notes

**Example Release URL:**
```
https://github.com/cpike5/discordbot/releases/tag/v0.3.0
```

### Step 6: Update Documentation (If Needed)

For MAJOR or significant MINOR releases, update relevant documentation:

- **README.md:** Update version references and compatibility notes
- **CLAUDE.md:** Update current version in Project Overview section
- **docs/articles/*.md:** Update version-specific documentation

---

## Continuous Integration (CI) Builds

The CI workflow runs on every push to `main` and for all pull requests, ensuring code quality and test coverage.

### CI Workflow Configuration

**Workflow File:** `.github/workflows/ci.yml`

**Triggers:**
- Push to `main` branch
- Pull requests targeting `main` branch

**Build Versioning:**

**Main Branch Builds:**
```bash
dotnet build --configuration Release --no-restore
# Uses version from Directory.Build.props
```

**Pull Request Builds:**
```bash
COMMIT_SHA=$(git rev-parse --short HEAD)
dotnet build --configuration Release --no-restore \
  /p:InformationalVersion="0.1.0-pr.123+abc1234"
```

**PR Version Format:** `{Version}-pr.{PR_NUMBER}+{COMMIT_SHA}`

Example: `0.1.0-pr.45+a3f2e1c`

### Test Execution and Reporting

CI workflow executes all tests with coverage collection:

```bash
dotnet test --configuration Release --no-build --verbosity normal \
  --logger "trx" --collect:"XPlat Code Coverage"
```

**Artifacts Uploaded:**
- Test results (`.trx` files) retained for 30 days
- Code coverage reports (Cobertura XML) retained for 30 days

---

## Branch Strategy

The repository follows a simplified trunk-based development workflow:

### Main Branch (`main`)

- **Purpose:** Primary development and release branch
- **Protection:** Pull request reviews required, status checks must pass
- **Release Strategy:** Releases are tagged from `main` branch
- **Commit Discipline:** All commits must pass CI tests

### Feature Branches

- **Naming Convention:** `feature/{issue-number}-{description}`
- **Examples:** `feature/issue-139-message-log-system`, `feature/oauth-integration`
- **Lifecycle:**
  1. Branch from `main`
  2. Implement feature with incremental commits
  3. Open pull request to `main`
  4. CI builds run automatically (with PR version metadata)
  5. Merge after review and passing tests
  6. Delete branch after merge

### Branch Protection Rules

**Configured on `main` branch:**
- Require pull request reviews before merging
- Require status checks to pass (CI workflow)
- Require branches to be up to date before merging
- No direct pushes to `main` (except version tags)

---

## Version Service Implementation

The `VersionService` provides centralized access to application version information throughout the codebase.

### Interface Definition

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Core\Interfaces\IVersionService.cs`

```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for retrieving application version information.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the current application version string.
    /// </summary>
    /// <returns>The version string (e.g., "v1.0.0"), or a fallback value if unavailable.</returns>
    string GetVersion();
}
```

### Service Implementation

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\VersionService.cs`

```csharp
using System.Reflection;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for retrieving application version information.
/// </summary>
public class VersionService : IVersionService
{
    private readonly string _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionService"/> class.
    /// </summary>
    public VersionService()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly?.GetName().Version?.ToString()
            ?? "0.0.0";

        // Strip any metadata suffix (e.g., "+abc123" from semantic versioning)
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        _version = $"v{version}";
    }

    /// <inheritdoc/>
    public string GetVersion() => _version;
}
```

### Dependency Injection Registration

**Location:** `C:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Program.cs`

```csharp
// Register version service as singleton
builder.Services.AddSingleton<IVersionService, VersionService>();
```

### Usage Examples

**Razor Pages:**
```cshtml
@inject DiscordBot.Core.Interfaces.IVersionService VersionService

<div class="sidebar-status">
  <span class="ml-auto text-xs text-text-tertiary">@VersionService.GetVersion()</span>
</div>
```

**Controllers/Services:**
```csharp
public class BotControlPageModel : PageModel
{
    private readonly IVersionService _versionService;

    public BotControlPageModel(IVersionService versionService)
    {
        _versionService = versionService;
    }

    public void OnGet()
    {
        var version = _versionService.GetVersion(); // Returns "v0.2.0"
    }
}
```

---

## Troubleshooting

### Common Issues and Solutions

#### Issue: Release Workflow Fails to Trigger

**Symptoms:**
- Pushed version tag, but no release workflow runs

**Solution:**
1. Verify tag follows naming convention (`v{MAJOR}.{MINOR}.{PATCH}`)
2. Check tag was pushed to remote: `git push origin v0.3.0`
3. Ensure tag is annotated: `git tag -a v0.3.0 -m "Release v0.3.0"`
4. Verify workflow file exists: `.github/workflows/release.yml`

#### Issue: Version Mismatch Between Directory.Build.props and Tag

**Symptoms:**
- Tag version doesn't match version in `Directory.Build.props`
- Confusing version displayed in UI

**Solution:**
1. Ensure `Directory.Build.props` is updated **before** creating tag
2. Commit version change separately from tag push
3. Verify commit with version update is tagged

**Correct Workflow:**
```bash
# 1. Update Directory.Build.props to 0.3.0
git add Directory.Build.props
git commit -m "chore: bump version to 0.3.0"

# 2. Create tag on the version bump commit
git tag -a v0.3.0 -m "Release v0.3.0"

# 3. Push commit and tag together
git push origin main v0.3.0
```

#### Issue: CI Build Shows Wrong Version in PR

**Symptoms:**
- PR build version doesn't include PR number or commit hash

**Solution:**
- This is expected for main branch builds
- PR builds should show: `0.1.0-pr.{NUMBER}+{COMMIT}`
- Main branch builds show: `0.2.0` (from Directory.Build.props)

#### Issue: VersionService Returns "v0.0.0" in Deployed Application

**Symptoms:**
- Admin UI sidebar shows "v0.0.0" instead of actual version

**Possible Causes:**
1. Assembly metadata not embedded during build
2. Entry assembly not found (rare)

**Solution:**
1. Verify `Directory.Build.props` is in solution root
2. Rebuild solution: `dotnet clean && dotnet build`
3. Check assembly metadata with reflection:
   ```csharp
   var assembly = Assembly.GetEntryAssembly();
   var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
   ```

#### Issue: Uncommitted Changes Prevent Tag Push

**Symptoms:**
- Git refuses to push tag due to uncommitted changes

**Solution:**
```bash
# Stash uncommitted changes
git stash

# Push tag
git push origin v0.3.0

# Restore stashed changes
git stash pop
```

---

## Best Practices

### 1. Update Version in Directory.Build.props First

Always update `Directory.Build.props` **before** creating and pushing the version tag. This ensures consistency between tagged code and embedded version metadata.

### 2. Use Annotated Tags for Releases

Annotated tags include metadata (author, date, message) and are recommended for releases:

```bash
# Annotated tag (recommended)
git tag -a v0.3.0 -m "Release v0.3.0"

# Lightweight tag (NOT recommended for releases)
git tag v0.3.0
```

### 3. Follow Conventional Commit Messages

Use conventional commit format for version bump commits:

```
chore: bump version to X.Y.Z
```

This maintains consistency and enables automated changelog generation in the future.

### 4. Test Locally Before Creating Release Tag

Before pushing a version tag, verify the build succeeds locally:

```bash
# Clean build
dotnet clean
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Verify version service
dotnet run --project src/DiscordBot.Bot
```

### 5. Document Breaking Changes in Release Notes

For MAJOR version releases, manually edit GitHub release notes to document breaking changes and migration steps.

**Example Release Notes:**
```markdown
## Breaking Changes

- **API Endpoint Renamed:** `/api/guilds/{id}` → `/api/servers/{id}`
  - Migration: Update API client to use new endpoint

- **Database Schema Change:** `Users.DiscordId` column renamed to `Users.DiscordUserId`
  - Migration: Run migration script: `dotnet ef database update`

## New Features

- Added message logging system with retention policies
- Implemented OAuth2 authentication with Discord

## Bug Fixes

- Fixed rate limiting on bot commands
- Corrected timezone handling in analytics dashboard
```

### 6. Coordinate MAJOR Releases with Team

MAJOR version releases (breaking changes) should be coordinated with all stakeholders:

- Notify users in Discord announcements channel
- Update deployment documentation
- Provide migration scripts or guides
- Schedule downtime window if database migrations required

### 7. Use Pre-Release Versions for Beta Testing

For testing significant changes before stable release:

```bash
# Create pre-release version
git tag -a v1.0.0-beta.1 -m "Beta release for v1.0.0"
git push origin v1.0.0-beta.1
```

GitHub automatically marks releases with hyphens as pre-release.

---

## Future Enhancements

### Planned Improvements

1. **Automated Changelog Generation**
   - Parse conventional commits to generate changelogs automatically
   - Include in GitHub release notes

2. **Version Display in Discord Bot**
   - Add `/version` slash command to display bot version
   - Include in embed footers for transparency

3. **Release Notes Template**
   - Create structured template for release notes
   - Standardize breaking changes, features, and fixes sections

4. **Rollback Procedures**
   - Document process for rolling back failed deployments
   - Include database migration rollback scripts

5. **Semantic Release Automation**
   - Investigate tools like `semantic-release` for fully automated versioning
   - Automatically determine version bump based on commit messages

---

## Related Documentation

- [Semantic Versioning 2.0.0 Specification](https://semver.org/)
- [Environment Configuration](environment-configuration.md) - Environment-specific settings
- [Identity Configuration](identity-configuration.md) - Authentication setup
- [Authorization Policies](authorization-policies.md) - Role-based access control
- GitHub Actions Workflows:
  - `.github/workflows/release.yml` - Automated release process
  - `.github/workflows/ci.yml` - Continuous integration builds

---

## Appendix: Version History

| Version | Release Date | Type | Notable Changes |
|---------|--------------|------|-----------------|
| v0.2.0 | 2024-12-24 | Minor | Message log system, OAuth integration, command analytics |
| v0.1.0 | 2024-11-15 | Minor | Initial pre-release with core bot functionality |

**Current Version:** v0.2.0
**Target Stable Release:** v1.0.0 (pending core feature completion)
