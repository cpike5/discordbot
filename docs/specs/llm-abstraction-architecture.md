# LLM Abstraction Architecture

## Overview

This specification defines the LLM-agnostic abstraction layer for the Discord bot's AI assistant feature. The architecture enables swapping LLM providers (Anthropic Claude, OpenAI, local models, etc.) without changing application code.

**Key Benefits:**

- **Provider flexibility** - Switch between LLM providers through configuration
- **Future-proof architecture** - Add new providers without modifying existing code
- **Testability** - Mock implementations for unit and integration testing
- **Cost optimization** - Route requests to different providers based on cost/performance
- **Compliance** - Support on-premise or region-specific LLM deployments

## Architecture Diagram

```
┌─────────────────────────────────────────────────────┐
│              AssistantService                       │
│  - Orchestrates agent lifecycle                     │
│  - Manages conversation context                     │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│              IAgentRunner                           │
│  - Implements agentic loop                          │
│  - Handles tool use cycles                          │
│  - Manages conversation history                     │
└──────────────────┬──────────────────────────────────┘
                   │
     ┌─────────────┼──────────────┐
     │             │              │
     ▼             ▼              ▼
┌─────────┐  ┌──────────────┐  ┌──────────────┐
│IToolRegistry│  │IPromptTemplate│  │ILlmClient    │
│             │  │               │  │              │
│ - Manage    │  │ - Load/subs   │  │ - Anthropic  │
│   tools     │  │   prompts     │  │ - OpenAI     │
│ - Execute   │  │               │  │ - Local      │
│   tools     │  │               │  │ - Custom     │
└─────────────┘  └──────────────┘  └──────────────┘
     │                                      │
     └──────────────────┬───────────────────┘
                        │
              ┌─────────▼──────────┐
              │  LLM Provider      │
              │  (Anthropic/OAI)   │
              └────────────────────┘
```

## Core Interfaces

### ILlmClient

Provider-agnostic interface for LLM completion calls. Abstracts away provider-specific API details.

```csharp
namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Represents a client for communicating with an LLM provider.
/// Implementations handle provider-specific API details and message mapping.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends a completion request to the LLM provider.
    /// </summary>
    /// <param name="request">The completion request with messages, tools, and parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The LLM response with content, tool calls, and usage information</returns>
    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of the LLM provider (e.g., "Anthropic", "OpenAI")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indicates whether this provider supports tool/function calling
    /// </summary>
    bool SupportsToolUse { get; }

    /// <summary>
    /// Indicates whether this provider supports prompt caching for performance optimization
    /// </summary>
    bool SupportsPromptCaching { get; }
}
```

### IAgentRunner

Orchestrates the agentic loop, managing the interaction between the LLM and tools.

```csharp
namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Manages the agentic loop - the cycle of LLM calls and tool execution.
/// Handles conversation history and tool use iterations.
/// </summary>
public interface IAgentRunner
{
    /// <summary>
    /// Runs an agentic loop for a single user message.
    /// Continues calling the LLM and executing tools until a final response is reached.
    /// </summary>
    /// <param name="userMessage">The user's message to process</param>
    /// <param name="context">Agent execution context (tools, prompts, configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The final response with content, tool calls, and metrics</returns>
    Task<AgentRunResult> RunAsync(
        string userMessage,
        AgentContext context,
        CancellationToken cancellationToken = default);
}
```

### IToolProvider

Groups related tools and handles their execution. Tools are organized by domain (Discord info, user data, etc.).

```csharp
namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Represents a collection of related tools that can be executed by an agent.
/// Providers organize tools by domain/purpose and handle their execution.
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// Gets the unique name of this tool provider (e.g., "DiscordTools")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a human-readable description of what tools this provider offers
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Returns the list of tools this provider offers.
    /// Tools are defined using the LLM provider's format.
    /// </summary>
    IEnumerable<LlmToolDefinition> GetTools();

    /// <summary>
    /// Executes a tool with the given input.
    /// </summary>
    /// <param name="toolName">The name of the tool to execute</param>
    /// <param name="input">The input parameters as a JSON element</param>
    /// <param name="context">Execution context with guild, user, and channel information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result object containing success/failure and output data</returns>
    Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
```

### IToolRegistry

Manages tool providers with enable/disable capability and routes tool execution.

```csharp
namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Central registry for managing tool providers.
/// Handles provider registration, enable/disable logic, and tool execution routing.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a tool provider with optional enabled state
    /// </summary>
    void RegisterProvider(IToolProvider provider, bool enabled = true);

    /// <summary>
    /// Enables a previously registered provider by name
    /// </summary>
    void EnableProvider(string providerName);

    /// <summary>
    /// Disables a previously registered provider by name
    /// </summary>
    void DisableProvider(string providerName);

    /// <summary>
    /// Gets all tools from enabled providers
    /// </summary>
    IEnumerable<LlmToolDefinition> GetEnabledTools();

    /// <summary>
    /// Executes a tool by searching enabled providers in registration order.
    /// First provider that has the tool handles execution.
    /// </summary>
    /// <param name="toolName">The tool to execute</param>
    /// <param name="input">Tool input parameters</param>
    /// <param name="context">Execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default);
}
```

### IPromptTemplate

Manages loading and substitution of agent prompt templates.

```csharp
namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Handles loading and customizing prompt templates for agents.
/// Supports dynamic placeholder substitution for context-specific prompts.
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// Loads a prompt template from storage (file, database, etc.)
    /// </summary>
    /// <param name="templatePath">The path or identifier of the template to load</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The template content as a string</returns>
    Task<string> LoadAsync(string templatePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Substitutes placeholders in a template with actual values
    /// </summary>
    /// <param name="template">The template string with {{placeholder}} markers</param>
    /// <param name="placeholders">Dictionary of placeholder names to values</param>
    /// <returns>The template with all placeholders replaced</returns>
    string Substitute(string template, Dictionary<string, string> placeholders);
}
```

## Message Format DTOs

### LlmMessage

Represents a single message in the conversation.

```csharp
namespace DiscordBot.Core.DTOs.LLM;

public class LlmMessage
{
    /// <summary>
    /// The role of who sent this message (User, Assistant, System)
    /// </summary>
    public LlmRole Role { get; set; }

    /// <summary>
    /// The text content of the message
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Tool calls made by the assistant in this message (if any)
    /// </summary>
    public List<LlmToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Results from tool executions (in response to assistant's tool calls)
    /// </summary>
    public List<LlmToolResult>? ToolResults { get; set; }
}

public enum LlmRole
{
    User,
    Assistant,
    System
}
```

### LlmRequest

Request sent to the LLM provider.

```csharp
namespace DiscordBot.Core.DTOs.LLM;

public class LlmRequest
{
    /// <summary>
    /// The system prompt that establishes the agent's behavior and constraints
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// The conversation history including user messages and assistant responses
    /// </summary>
    public List<LlmMessage> Messages { get; set; } = new();

    /// <summary>
    /// Available tools the LLM can call (null if tools not supported)
    /// </summary>
    public List<LlmToolDefinition>? Tools { get; set; }

    /// <summary>
    /// Maximum tokens to generate in the response
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Sampling temperature (0-1). Lower = more deterministic, higher = more creative
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Whether to enable prompt caching if the provider supports it
    /// </summary>
    public bool EnablePromptCaching { get; set; } = true;
}
```

### LlmResponse

Response from the LLM provider.

```csharp
namespace DiscordBot.Core.DTOs.LLM;

public class LlmResponse
{
    /// <summary>
    /// Indicates whether the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The text content of the response (null if tool use or error)
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Why the model stopped generating (end turn, tool use, max tokens, error, etc.)
    /// </summary>
    public LlmStopReason StopReason { get; set; }

    /// <summary>
    /// Tool calls made by the model (if StopReason == ToolUse)
    /// </summary>
    public List<LlmToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Token usage metrics for monitoring and cost tracking
    /// </summary>
    public LlmUsage Usage { get; set; } = new();

    /// <summary>
    /// Error message if Success is false
    /// </summary>
    public string? ErrorMessage { get; set; }
}

public enum LlmStopReason
{
    /// <summary>The model reached a natural conclusion (end of turn)</summary>
    EndTurn,

    /// <summary>The model wants to call a tool</summary>
    ToolUse,

    /// <summary>The response hit the max tokens limit</summary>
    MaxTokens,

    /// <summary>The model had an error or couldn't generate</summary>
    Error
}

public class LlmUsage
{
    /// <summary>Number of tokens in the input (request)</summary>
    public int InputTokens { get; set; }

    /// <summary>Number of tokens in the output (response)</summary>
    public int OutputTokens { get; set; }

    /// <summary>Total tokens for billing/monitoring purposes</summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>Optional: estimated cost in USD if provider supports billing calculation</summary>
    public decimal? EstimatedCost { get; set; }
}
```

### LlmToolDefinition

Definition of a tool that the LLM can call.

```csharp
namespace DiscordBot.Core.DTOs.LLM;

public class LlmToolDefinition
{
    /// <summary>
    /// Unique identifier for this tool (e.g., "get_user_roles")
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// JSON Schema defining the input parameters
    /// </summary>
    public JsonElement InputSchema { get; set; }
}

public class LlmToolCall
{
    /// <summary>
    /// Unique ID for this tool call (assigned by the LLM provider)
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The name of the tool being called
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The input parameters as JSON
    /// </summary>
    public JsonElement Input { get; set; }
}

public class LlmToolResult
{
    /// <summary>
    /// The ID of the tool call this result responds to
    /// </summary>
    public string ToolCallId { get; set; }

    /// <summary>
    /// The result of executing the tool (success data or error)
    /// </summary>
    public JsonElement Content { get; set; }

    /// <summary>
    /// Whether the tool executed successfully
    /// </summary>
    public bool IsError { get; set; }
}

public class ToolContext
{
    /// <summary>
    /// The Discord user ID making the request
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The Discord guild (server) ID for context
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// The Discord channel ID for context
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// User's roles in the guild (for permission checks)
    /// </summary>
    public List<string> UserRoles { get; set; } = new();
}

public class ToolExecutionResult
{
    /// <summary>
    /// Whether the tool executed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The output data as JSON
    /// </summary>
    public JsonElement Output { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

public class AgentContext
{
    /// <summary>
    /// The system prompt defining agent behavior
    /// </summary>
    public string SystemPrompt { get; set; }

    /// <summary>
    /// Tool registry for this agent session
    /// </summary>
    public IToolRegistry ToolRegistry { get; set; }

    /// <summary>
    /// Execution context with user/guild/channel information
    /// </summary>
    public ToolContext ExecutionContext { get; set; }

    /// <summary>
    /// Maximum tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Temperature for generation
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}

public class AgentRunResult
{
    /// <summary>
    /// Whether the agent run succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The final response text from the agent
    /// </summary>
    public string Response { get; set; }

    /// <summary>
    /// Number of agentic loops executed (tool use iterations)
    /// </summary>
    public int LoopCount { get; set; }

    /// <summary>
    /// Aggregate token usage across all LLM calls in this run
    /// </summary>
    public LlmUsage TotalUsage { get; set; } = new();

    /// <summary>
    /// Error message if the run failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

## Anthropic Implementation

### AnthropicLlmClient

Implements `ILlmClient` using the Anthropic SDK. Handles mapping between LLM DTOs and Anthropic API format.

**File Location:** `src/DiscordBot.Infrastructure/Services/LLM/Anthropic/AnthropicLlmClient.cs`

**Key Responsibilities:**

- Maps `LlmRequest` → `MessageCreateParams`
- Maps `MessageResponse` → `LlmResponse`
- Handles message/tool block structures from Anthropic API
- Enables prompt caching via `cache_control` when requested
- Implements retry logic with exponential backoff
- Tracks token usage and costs
- Handles streaming responses (if implemented)

**Configuration via AnthropicOptions:**

```csharp
public class AnthropicOptions
{
    public string ApiKey { get; set; }
    public string DefaultModel { get; set; } = "claude-3-5-sonnet-20241022";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 300;
}
```

## Agentic Loop Flow

The `AgentRunner` implementation orchestrates the interaction between the LLM and tools:

```
┌──────────────────────────────────────┐
│ 1. User message + context            │
└──────────────┬───────────────────────┘
               │
┌──────────────▼───────────────────────┐
│ 2. Load system prompt + substitutions│
└──────────────┬───────────────────────┘
               │
┌──────────────▼───────────────────────┐
│ 3. Get enabled tools from registry    │
└──────────────┬───────────────────────┘
               │
┌──────────────▼───────────────────────┐
│ 4. Call ILlmClient.CompleteAsync()   │
│    with messages + tools              │
└──────────────┬───────────────────────┘
               │
               ├─────────────────────────────┐
               │                             │
               ▼                             ▼
         StopReason:              StopReason:
         ToolUse                  EndTurn/Error
         │                        │
         │                        └──────────────────┐
         │                                           │
         ▼                                           ▼
    ┌─────────────────────┐        ┌────────────────────────┐
    │ 5. Execute tools    │        │ 7. Return final        │
    │ via registry        │        │    response            │
    └─────────┬───────────┘        └────────────────────────┘
              │
    ┌─────────▼───────────┐
    │ 6. Add results to   │
    │    conversation &   │
    │    loop back to 4   │
    └─────────────────────┘
```

**Algorithm:**

1. User message received with guild/user/channel context
2. Load system prompt template, substitute placeholders (guild name, user info, etc.)
3. Get all enabled tools from `IToolRegistry.GetEnabledTools()`
4. Call `ILlmClient.CompleteAsync()` with:
   - System prompt
   - Conversation history
   - Available tools
   - Parameters (max tokens, temperature, etc.)
5. Check response `StopReason`:
   - **ToolUse**: Execute each tool call via `ToolRegistry.ExecuteToolAsync()`
     - Add tool results to conversation as user message
     - Increment loop counter
     - Loop back to step 4
   - **EndTurn**: Extract text content and return
   - **MaxTokens**: Log warning and return partial response
   - **Error**: Return error status
6. Track metrics: total tokens, cost estimate, loop count
7. Return `AgentRunResult` with final response

## Adding a New LLM Provider

### Step 1: Create Provider Directory

```
src/DiscordBot.Infrastructure/Services/LLM/{ProviderName}/
├── {Provider}LlmClient.cs
└── {Provider}MessageMapper.cs
```

### Step 2: Implement ILlmClient

Example: OpenAI provider

```csharp
namespace DiscordBot.Infrastructure.Services.LLM.OpenAI;

public class OpenAILlmClient : ILlmClient
{
    private readonly OpenAIClient _client;
    private readonly ILogger<OpenAILlmClient> _logger;
    private readonly OpenAIOptions _options;

    public OpenAILlmClient(
        OpenAIClient client,
        IOptions<OpenAIOptions> options,
        ILogger<OpenAILlmClient> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderName => "OpenAI";
    public bool SupportsToolUse => true;
    public bool SupportsPromptCaching => false;  // OpenAI doesn't have prompt caching yet

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Map LlmRequest to OpenAI ChatCompletionCreateParams
            var params = new ChatCompletionCreateParams
            {
                Model = _options.DefaultModel,
                Messages = OpenAIMessageMapper.ToOpenAIMessages(request),
                Tools = request.Tools?.Any() == true
                    ? OpenAIMessageMapper.ToOpenAITools(request.Tools)
                    : null,
                MaxTokens = request.MaxTokens,
                Temperature = (float)request.Temperature
            };

            // Call OpenAI API
            var response = await _client.Chat.Completions.CreateAsync(params, cancellationToken);

            // Map response back to LlmResponse
            return OpenAIMessageMapper.ToLlmResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI completion request failed");
            return new LlmResponse
            {
                Success = false,
                StopReason = LlmStopReason.Error,
                ErrorMessage = ex.Message
            };
        }
    }
}
```

### Step 3: Create Message Mapper

```csharp
namespace DiscordBot.Infrastructure.Services.LLM.OpenAI;

public static class OpenAIMessageMapper
{
    public static List<ChatCompletionMessageParam> ToOpenAIMessages(LlmRequest request)
    {
        var messages = new List<ChatCompletionMessageParam>();

        // System prompt
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(new SystemMessage { Content = request.SystemPrompt });
        }

        // Conversation history
        foreach (var msg in request.Messages)
        {
            messages.Add(msg.Role switch
            {
                LlmRole.User => new UserMessage { Content = msg.Content },
                LlmRole.Assistant => new AssistantMessage { Content = msg.Content },
                _ => throw new ArgumentException($"Unsupported role: {msg.Role}")
            });
        }

        return messages;
    }

    public static List<Tool> ToOpenAITools(List<LlmToolDefinition> tools)
    {
        return tools.Select(t => new Tool
        {
            Function = new FunctionDefinition
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.InputSchema
            }
        }).ToList();
    }

    public static LlmResponse ToLlmResponse(ChatCompletionResponse response)
    {
        var choice = response.Choices.FirstOrDefault();
        if (choice == null)
            return new LlmResponse { Success = false, ErrorMessage = "No choices in response" };

        // Map stop reason
        var stopReason = choice.FinishReason switch
        {
            FinishReason.Stop => LlmStopReason.EndTurn,
            FinishReason.ToolCalls => LlmStopReason.ToolUse,
            FinishReason.Length => LlmStopReason.MaxTokens,
            _ => LlmStopReason.Error
        };

        // Extract tool calls if present
        List<LlmToolCall>? toolCalls = null;
        if (choice.Message.ToolCalls?.Any() == true)
        {
            toolCalls = choice.Message.ToolCalls.Select(tc => new LlmToolCall
            {
                Id = tc.Id,
                Name = tc.Function.Name,
                Input = JsonSerializer.Deserialize<JsonElement>(tc.Function.Arguments)
            }).ToList();
        }

        return new LlmResponse
        {
            Success = true,
            Content = choice.Message.Content,
            StopReason = stopReason,
            ToolCalls = toolCalls,
            Usage = new LlmUsage
            {
                InputTokens = response.Usage.PromptTokens,
                OutputTokens = response.Usage.CompletionTokens
            }
        };
    }
}
```

### Step 4: Register in Dependency Injection

In `Program.cs`:

```csharp
// Configure OpenAI client
services.Configure<OpenAIOptions>(configuration.GetSection("OpenAI"));

services.AddSingleton<OpenAIClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAIOptions>>();
    return new OpenAIClient(new ApiKeyCredential(options.Value.ApiKey));
});

// Register provider
services.AddSingleton<ILlmClient>(sp =>
{
    var provider = configuration.GetValue<string>("Assistant:LlmProvider", "Anthropic");

    return provider switch
    {
        "OpenAI" => sp.GetRequiredService<OpenAILlmClient>(),
        "Anthropic" => sp.GetRequiredService<AnthropicLlmClient>(),
        _ => throw new InvalidOperationException($"Unknown LLM provider: {provider}")
    };
});

// Register agent runner
services.AddScoped<IAgentRunner, AgentRunner>();
```

### Step 5: Configure Provider in appsettings.json

```json
{
  "Assistant": {
    "LlmProvider": "OpenAI",  // Switch between "Anthropic" and "OpenAI"
    "MaxTokens": 2048,
    "Temperature": 0.7
  },
  "OpenAI": {
    "ApiKey": "sk-...",  // Use user secrets in development
    "DefaultModel": "gpt-4-turbo"
  }
}
```

## Configuration

### Application Configuration

Add to `appsettings.json`:

```json
{
  "Assistant": {
    "LlmProvider": "Anthropic",
    "MaxTokens": 2048,
    "Temperature": 0.7,
    "EnablePromptCaching": true
  },
  "Anthropic": {
    "ApiKey": "sk-ant-...",
    "DefaultModel": "claude-3-5-sonnet-20241022",
    "MaxRetries": 3,
    "TimeoutSeconds": 300
  },
  "OpenAI": {
    "ApiKey": "sk-...",
    "DefaultModel": "gpt-4-turbo"
  }
}
```

### Options Classes

```csharp
namespace DiscordBot.Core.Configuration;

public class AssistantOptions
{
    public string LlmProvider { get; set; } = "Anthropic";
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public bool EnablePromptCaching { get; set; } = true;
}

public class AnthropicOptions
{
    public string ApiKey { get; set; }
    public string DefaultModel { get; set; } = "claude-3-5-sonnet-20241022";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 300;
}

public class OpenAIOptions
{
    public string ApiKey { get; set; }
    public string DefaultModel { get; set; } = "gpt-4-turbo";
}
```

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Interface-based abstraction** | Enables DI-based provider swapping; implementations can be registered at runtime based on configuration |
| **Separate ILlmClient from IAgentRunner** | Single responsibility principle; completion calls are orthogonal to orchestration logic |
| **Tool provider pattern** | Groups related tools; enables per-agent tool whitelists; easier to manage authorization |
| **Async throughout** | LLM API calls are I/O-bound; async/await is essential for scalability |
| **JsonElement for tool inputs** | Provider-agnostic JSON handling; avoids serialization roundtrips |
| **ToolContext for execution** | Passes user/guild/channel info through the tool pipeline for permission checks |
| **Prompt template abstraction** | Decouples prompt definitions from code; enables runtime prompt changes without recompilation |
| **Structured DTOs** | Clear contracts between layers; enables testing with mock implementations |

## File Structure

```
src/DiscordBot.Core/
├── Interfaces/LLM/
│   ├── ILlmClient.cs
│   ├── IAgentRunner.cs
│   ├── IToolProvider.cs
│   ├── IToolRegistry.cs
│   └── IPromptTemplate.cs
├── DTOs/LLM/
│   ├── LlmMessage.cs
│   ├── LlmRequest.cs
│   ├── LlmResponse.cs
│   ├── LlmToolDefinition.cs
│   ├── LlmToolCall.cs
│   ├── LlmToolResult.cs
│   ├── ToolContext.cs
│   ├── ToolExecutionResult.cs
│   ├── AgentContext.cs
│   ├── AgentRunResult.cs
│   └── Enums/
│       ├── LlmRole.cs
│       └── LlmStopReason.cs
└── Configuration/
    ├── AssistantOptions.cs
    ├── AnthropicOptions.cs
    └── OpenAIOptions.cs

src/DiscordBot.Infrastructure/
├── Services/LLM/
│   ├── AgentRunner.cs
│   ├── ToolRegistry.cs
│   ├── PromptTemplate.cs
│   ├── Anthropic/
│   │   ├── AnthropicLlmClient.cs
│   │   └── AnthropicMessageMapper.cs
│   └── OpenAI/  (Future)
│       ├── OpenAILlmClient.cs
│       └── OpenAIMessageMapper.cs
```

## Testing Strategy

### Mock ILlmClient for Unit Tests

```csharp
public class MockLlmClient : ILlmClient
{
    private readonly string _responseContent;

    public MockLlmClient(string responseContent = "Mock response")
    {
        _responseContent = responseContent;
    }

    public string ProviderName => "Mock";
    public bool SupportsToolUse => true;
    public bool SupportsPromptCaching => false;

    public Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LlmResponse
        {
            Success = true,
            Content = _responseContent,
            StopReason = LlmStopReason.EndTurn,
            Usage = new LlmUsage { InputTokens = 100, OutputTokens = 50 }
        });
    }
}
```

### Mock IToolProvider for Unit Tests

```csharp
public class MockToolProvider : IToolProvider
{
    public string Name => "Mock";
    public string Description => "Mock tool provider for testing";

    public IEnumerable<LlmToolDefinition> GetTools()
    {
        yield return new LlmToolDefinition
        {
            Name = "mock_tool",
            Description = "A mock tool",
            InputSchema = JsonSerializer.Deserialize<JsonElement>("{\"type\": \"object\"}")
        };
    }

    public Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolExecutionResult
        {
            Success = true,
            Output = JsonSerializer.Deserialize<JsonElement>("{\"result\": \"success\"}")
        });
    }
}
```

## Future Considerations

- **Streaming responses**: Add `CompleteStreamAsync()` for real-time token streaming to Discord
- **Cost tracking**: Integrate with billing systems to track per-provider costs
- **Load balancing**: Route requests to different providers based on cost/speed tradeoffs
- **Regional deployment**: Support region-specific or on-premise LLM endpoints
- **Fine-tuning**: Support provider-specific fine-tuned models
- **Fallback chains**: Automatically fall back to alternative providers on failure
- **Token budget**: Implement token budgets per guild/user to prevent runaway costs

## References

- [assistant-tool-catalog.md](assistant-tool-catalog.md) - Complete tool definitions and implementation phases
- [assistant-requirements.md](../requirements/assistant-requirements.md) - Feature requirements and decisions
- [assistant-implementation-plan.md](../requirements/assistant-implementation-plan.md) - Implementation phases and file manifest
