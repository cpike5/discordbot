using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for DM assistant memory/notes management.
/// </summary>
public static class MemoryTools
{
    public const string SaveNote = "save_note";
    public const string SearchNotes = "search_notes";
    public const string GetNote = "get_note";
    public const string ListNotes = "list_notes";
    public const string DeleteNote = "delete_note";

    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateSaveNoteTool();
        yield return CreateSearchNotesTool();
        yield return CreateGetNoteTool();
        yield return CreateListNotesTool();
        yield return CreateDeleteNoteTool();
    }

    private static LlmToolDefinition CreateSaveNoteTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "content": {
                        "type": "string",
                        "description": "The note content to save. Can be a preference, fact, reminder, or any information the user wants remembered."
                    },
                    "tag": {
                        "type": "string",
                        "description": "Optional category tag for organizing notes (e.g., 'preference', 'fact', 'context', 'todo')."
                    }
                },
                "required": ["content"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = SaveNote,
            Description = "Saves a personal note for the user that persists across conversations. Use this when the user asks you to remember something, states a preference, or shares important context they want retained. Notes are private to each user.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateSearchNotesTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "Search term to find in note content or tags."
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of results to return. Default is 10.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 50
                    }
                },
                "required": ["query"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = SearchNotes,
            Description = "Searches the user's saved notes by keyword. Matches against both note content and tags. Use this to recall previously saved information about the user.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateGetNoteTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "note_id": {
                        "type": "integer",
                        "description": "The ID of the note to retrieve."
                    }
                },
                "required": ["note_id"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetNote,
            Description = "Retrieves a specific note by its ID. Use this when you need the full content of a particular note.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateListNotesTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "tag": {
                        "type": "string",
                        "description": "Optional tag to filter notes by category."
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of notes to return. Default is 20.",
                        "default": 20,
                        "minimum": 1,
                        "maximum": 50
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = ListNotes,
            Description = "Lists the user's saved notes, optionally filtered by tag. Returns notes sorted by most recently updated. Use this to browse all saved notes or notes in a specific category.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateDeleteNoteTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "note_id": {
                        "type": "integer",
                        "description": "The ID of the note to delete."
                    }
                },
                "required": ["note_id"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = DeleteNote,
            Description = "Deletes a specific note by its ID. Use this when the user wants to remove previously saved information. This action cannot be undone.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
