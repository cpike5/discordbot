# Commands Page

The Commands page is an admin UI feature that displays all registered slash command modules from Discord.NET's InteractionService. It provides a comprehensive view of command metadata including parameters, preconditions, and module organization.

## Overview

**URL:** `/Commands`

**Authorization:** Requires authentication (Viewer role or higher)

**Purpose:** View and browse all slash commands registered in the bot's InteractionService, organized by module with detailed parameter and precondition information.

---

## Features

### Module Organization

Commands are organized into collapsible module cards, making it easy to browse large command libraries:

- **Module Header**: Displays module name, description, and command count
- **Expand/Collapse**: Click any module to reveal or hide its commands
- **Command Grouping**: Grouped commands (e.g., `/consent grant`) display with their full path

### Command Details

Each command displays comprehensive metadata:

- **Command Name**: Full slash command name with group prefix if applicable
- **Description**: Help text describing the command's purpose
- **Parameters**: All command parameters with type, required/optional status, and descriptions
- **Preconditions**: Permission requirements, rate limits, and restrictions shown as color-coded badges

### Parameter Information

For each parameter, the page displays:

- **Name**: Parameter identifier used in the command
- **Type**: Data type (String, Integer, User, Channel, etc.)
- **Required/Optional**: Clear indicator of whether the parameter is mandatory
- **Description**: Help text explaining the parameter's purpose
- **Default Value**: Default value if parameter is optional (when applicable)
- **Choices**: Available enum values or predefined choices (when applicable)

### Precondition Badges

Preconditions are displayed as color-coded badges for quick identification:

| Precondition | Badge Color | Display |
|--------------|-------------|---------|
| RequireAdmin | Orange | "Admin" |
| RequireOwner | Red | "Owner Only" |
| RateLimit | Amber | Rate limit configuration (e.g., "5 per 60s (User)") |
| RequireBotPermission | Gray | Required bot permission name |
| RequireUserPermission | Blue | Required user permission name |
| RequireContext | Gray | Required context type (Guild/DM) |

### Empty State

When no commands are registered, the page displays a helpful message indicating the bot hasn't registered any slash commands yet.

---

## Technical Architecture

### Service Layer

**ICommandMetadataService** (`DiscordBot.Core.Interfaces`)

Service interface for extracting command metadata from Discord.NET's InteractionService:

```csharp
public interface ICommandMetadataService
{
    Task<IReadOnlyList<CommandModuleDto>> GetAllModulesAsync(
        CancellationToken cancellationToken = default);
}
```

The service implementation inspects the InteractionService at runtime to build a comprehensive metadata model of all registered commands.

### Data Transfer Objects

Command metadata is transferred using strongly-typed DTOs in `DiscordBot.Core.DTOs`:

#### CommandModuleDto

Represents a command module (class containing commands):

```csharp
public class CommandModuleDto
{
    public string Name { get; set; }              // Class name (e.g., "ConsentModule")
    public string DisplayName { get; set; }       // Formatted name (e.g., "Consent")
    public string? Description { get; set; }      // Module description
    public bool IsSlashGroup { get; set; }        // Whether module uses [Group] attribute
    public string? GroupName { get; set; }        // Group prefix (e.g., "consent")
    public List<CommandInfoDto> Commands { get; set; }
    public int CommandCount { get; }              // Total commands in module
}
```

#### CommandInfoDto

Represents an individual slash command:

```csharp
public class CommandInfoDto
{
    public string Name { get; set; }              // Command name (e.g., "grant")
    public string FullName { get; set; }          // Full path (e.g., "consent grant")
    public string Description { get; set; }       // Command description
    public List<CommandParameterDto> Parameters { get; set; }
    public List<PreconditionDto> Preconditions { get; set; }
    public string ModuleName { get; set; }        // Parent module name
}
```

#### CommandParameterDto

Represents a command parameter:

```csharp
public class CommandParameterDto
{
    public string Name { get; set; }              // Parameter name
    public string? Description { get; set; }      // Parameter description
    public string Type { get; set; }              // Friendly type name (e.g., "String")
    public bool IsRequired { get; set; }          // Required flag
    public string? DefaultValue { get; set; }     // Default value (if optional)
    public List<string>? Choices { get; set; }    // Available choices (for enums)
}
```

#### PreconditionDto

Represents a command precondition/restriction:

```csharp
public class PreconditionDto
{
    public string Name { get; set; }              // Precondition name
    public PreconditionType Type { get; set; }    // Precondition type enum
    public string? Configuration { get; set; }    // Human-readable config details
}
```

#### PreconditionType Enum

```csharp
public enum PreconditionType
{
    Admin,              // RequireAdminAttribute
    Owner,              // RequireOwnerAttribute
    RateLimit,          // RateLimitAttribute
    BotPermission,      // RequireBotPermission
    UserPermission,     // RequireUserPermission
    Context,            // RequireContext (Guild/DM)
    Custom              // Other custom preconditions
}
```

### ViewModels

Page-level ViewModels map DTOs to presentation-friendly models in `DiscordBot.Bot.ViewModels.Pages`:

- **CommandsListViewModel**: Top-level view model with module list and summary counts
- **CommandModuleViewModel**: Module-level view model with display formatting
- **CommandInfoViewModel**: Command-level view model with badge mappings
- **CommandParameterViewModel**: Parameter view model with type formatting

### Page Model

**IndexModel** (`Pages/Commands/Index.cshtml.cs`)

Razor Pages PageModel responsible for:

1. Injecting `ICommandMetadataService`
2. Fetching command metadata via `GetAllModulesAsync()`
3. Converting DTOs to ViewModels using `CommandsListViewModel.FromDtos()`
4. Exposing ViewModel to the Razor view

```csharp
[Authorize]
public class IndexModel : PageModel
{
    private readonly ICommandMetadataService _commandMetadataService;

    public CommandsListViewModel ViewModel { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);
        ViewModel = CommandsListViewModel.FromDtos(modules);
    }
}
```

---

## UI Components

The Commands page leverages the existing design system and shared components:

### Module Cards

Each module is rendered as a collapsible card with:

- **Header**: Module icon, name, description, command count badge
- **Chevron**: Rotate animation indicating expand/collapse state
- **Content Area**: Nested command list (hidden when collapsed)

### Command Items

Individual commands within modules display:

- **Command Icon**: Terminal/code icon
- **Command Name**: Monospace font with slash prefix (e.g., `/ping`)
- **Description**: Secondary text explaining command purpose
- **Precondition Badges**: Right-aligned badges for restrictions
- **Parameters Section**: Expandable parameter details

### Parameter Items

Parameter details use subtle background cards with:

- **Name**: Monospace font
- **Type Badge**: Small gray badge with type name
- **Required/Optional**: Color-coded indicator (red for required, gray for optional)
- **Description**: Small secondary text

### Precondition Badges

Badges follow the design system badge component with semantic colors:

- **Red (Error)**: Owner-only commands (most restrictive)
- **Orange**: Admin-required commands
- **Blue**: User permission requirements
- **Amber (Warning)**: Rate limits
- **Gray**: Bot permissions and context restrictions

---

## User Workflow

### Browsing Commands

1. Navigate to `/Commands` in the admin UI
2. View list of all command modules
3. Click any module header to expand and view commands
4. Review command details, parameters, and restrictions

### Finding Specific Commands

Users can quickly locate commands by:

- **Scanning module names**: Modules are organized logically (e.g., "Admin", "Consent", "Help")
- **Expanding relevant modules**: Click to reveal commands within
- **Reading descriptions**: Both module and command descriptions help identify functionality

### Understanding Requirements

Precondition badges make it immediately clear:

- **Who can use it**: Admin/Owner badges show permission level
- **Usage limits**: Rate limit badges show frequency restrictions
- **Required permissions**: User/bot permission badges show Discord permission needs
- **Context**: Guild/DM badges show where command can be used

---

## Integration Points

### InteractionService

The page reads directly from Discord.NET's `InteractionService`, which:

- Registers all slash command modules during bot startup
- Maintains metadata about commands, parameters, and preconditions
- Updates when commands are added/modified during development

### Command Registration

Commands must be properly registered in the InteractionService to appear:

1. Command modules inherit from `InteractionModuleBase<SocketInteractionContext>`
2. Commands use `[SlashCommand]` attribute
3. Modules are discovered and registered by `InteractionHandler` during startup
4. Registration happens globally or per-guild based on configuration

### Real-Time Updates

Command metadata is fetched on each page load, ensuring the display reflects the current state of registered commands. No caching is applied at the page level, though the underlying InteractionService maintains its own internal state.

---

## Use Cases

### For Developers

- **API Documentation**: Browse all available commands and their signatures
- **Integration Planning**: Understand command parameters and data types for bot integration
- **Permission Verification**: Confirm which commands require admin/owner permissions
- **Rate Limit Review**: Identify commands with usage restrictions

### For Server Administrators

- **Command Discovery**: Learn what commands are available in the bot
- **Permission Understanding**: See which commands require specific Discord permissions
- **User Training**: Reference when creating server documentation or user guides
- **Troubleshooting**: Verify command availability and requirements

### For Support Staff

- **User Assistance**: Quickly reference command parameters when helping users
- **Error Diagnosis**: Check if users have required permissions for commands
- **Documentation**: Generate support documentation from live command metadata

---

## Implementation Notes

### Performance

- Command metadata is fetched asynchronously via `GetAllModulesAsync()`
- No database queries - all data comes from in-memory InteractionService
- ViewModels are built on-demand per request (no caching at page level)
- Typical response time: < 100ms for 20-30 command modules

### Scalability

The design accommodates bots with large command libraries:

- Collapsible modules keep the UI manageable
- No pagination required - all modules load at once
- Module cards use CSS transitions for smooth expand/collapse
- Responsive design adapts to mobile, tablet, and desktop viewports

### Accessibility

The page implements accessibility best practices:

- Keyboard navigation with Tab/Enter/Space
- ARIA attributes for expand/collapse state (`aria-expanded`, `aria-controls`)
- Focus indicators on interactive elements
- Screen reader-friendly badge labels
- Semantic HTML structure with proper heading hierarchy

### Extensibility

The architecture supports future enhancements:

- **Search/Filter**: ViewModel structure supports filtering by module, precondition, or search term
- **Sorting**: Commands can be sorted alphabetically, by module, or by restriction level
- **Export**: Command metadata can be serialized to JSON/YAML for external tools
- **API Endpoint**: The same DTOs can power a REST API endpoint for programmatic access

---

## Related Documentation

- [Admin Commands](admin-commands.md) - Slash command reference and usage
- [Design System](design-system.md) - UI design tokens and component specifications
- [Commands Page Design](commands-page-design.md) - Detailed UI/UX design specification
- [Interactive Components](interactive-components.md) - Discord component patterns (buttons, menus)
- [Authorization Policies](authorization-policies.md) - Role hierarchy and access control

---

## Troubleshooting

### Commands Not Appearing

**Symptom:** Commands page is empty or missing expected commands

**Possible Causes:**
1. Commands not registered in InteractionService
2. InteractionHandler hasn't completed module discovery
3. Bot hasn't finished startup sequence

**Solutions:**
- Check bot logs for command registration errors
- Verify command modules inherit from `InteractionModuleBase<SocketInteractionContext>`
- Ensure `[SlashCommand]` attributes are properly configured
- Restart the bot to trigger fresh command registration

### Missing Parameter Information

**Symptom:** Parameters display without descriptions or type information

**Possible Causes:**
1. Command method parameters missing `[Summary]` attribute
2. Parameter types not recognized by Discord.NET type converter
3. Custom parameter types without proper metadata

**Solutions:**
- Add `[Summary("description")]` attributes to command parameters
- Use standard Discord.NET parameter types (string, int, IUser, IChannel, etc.)
- Verify custom type converters are registered with InteractionService

### Precondition Badges Not Showing

**Symptom:** Commands don't display expected permission badges

**Possible Causes:**
1. Precondition attributes not applied to command methods or modules
2. Custom preconditions not recognized by `CommandMetadataService`
3. Precondition mapping logic incomplete

**Solutions:**
- Verify `[RequireAdmin]`, `[RequireOwner]`, or `[RateLimit]` attributes are present
- Check that custom preconditions are mapped in `CommandMetadataService` implementation
- Review logs for precondition parsing errors

---

*Document Version: 1.0*
*Last Updated: December 2025*
*Status: Feature Complete (v0.2.1)*
