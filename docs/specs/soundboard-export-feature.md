# Soundboard Export Feature Specification

**Version:** 1.0
**Status:** Draft
**Target Release:** v0.18.0

## Overview

Add the ability to export all soundboard clips from a guild as a ZIP archive, including metadata manifest. This feature enables guild administrators to back up their soundboard or migrate sounds between systems.

## User Story

As a guild administrator, I want to export all my soundboard clips in a single download so that I can back them up or use them elsewhere.

## Requirements

### Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-1 | Export all sounds for a guild as a ZIP archive | Must |
| FR-2 | Include manifest.json with sound metadata | Must |
| FR-3 | Rename files from UUID to human-readable names | Must |
| FR-4 | Available from admin Guilds/Soundboard page | Must |
| FR-5 | Disable export when no sounds exist | Should |

### Non-Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| NFR-1 | Export should complete within 30 seconds for typical soundboards | Should |
| NFR-2 | Clean up temporary files after export | Must |
| NFR-3 | Follow existing authorization patterns (RequireViewer policy) | Must |

## Technical Design

### API Endpoint

```
GET /api/guilds/{guildId}/sounds/export
```

**Authorization:** `RequireViewer` policy (consistent with existing sounds endpoints)

**Response:**
- `200 OK` - Returns ZIP file as `application/zip`
- `404 Not Found` - Guild not found or no sounds to export
- `401/403` - Unauthorized

**Headers:**
```
Content-Type: application/zip
Content-Disposition: attachment; filename="soundboard-{guildId}-{yyyyMMdd-HHmmss}.zip"
```

### ZIP Archive Structure

```
soundboard-123456789-20260127-120000.zip
├── manifest.json
├── airhorn.mp3
├── sad-trombone.wav
├── victory.ogg
└── ...
```

### Manifest Schema

```json
{
  "exportedAt": "2026-01-27T12:00:00Z",
  "guildId": "123456789",
  "guildName": "My Discord Server",
  "totalSounds": 5,
  "totalSizeBytes": 1048576,
  "sounds": [
    {
      "id": "a1b2c3d4-e5f6-...",
      "name": "airhorn",
      "fileName": "airhorn.mp3",
      "originalFileName": "a1b2c3d4-e5f6-....mp3",
      "durationSeconds": 2.5,
      "fileSizeBytes": 51200,
      "playCount": 42,
      "uploadedById": "987654321",
      "uploadedAt": "2026-01-15T10:30:00Z"
    }
  ]
}
```

### Implementation Components

#### 1. SoundsController.cs

Add `ExportAllSoundsAsync` method:
- Retrieve all sounds via `ISoundService.GetAllByGuildAsync()`
- Create temporary directory
- Copy sound files with human-readable names
- Generate manifest.json
- Create ZIP using `System.IO.Compression.ZipFile`
- Return file result
- Clean up temp directory in finally block

#### 2. Index.cshtml (Guilds/Soundboard)

Add export button in header:
```razor
@if (Model.Sounds.Any())
{
    <a href="/api/guilds/@Model.GuildId/sounds/export"
       class="btn btn-secondary">
        Export All
    </a>
}
```

### File Naming Strategy

When exporting, files are renamed from UUID format to human-readable:
- Original: `a1b2c3d4-e5f6-7890-abcd-ef1234567890.mp3`
- Exported: `airhorn.mp3`

Handle duplicate names by appending suffix:
- `airhorn.mp3`, `airhorn-1.mp3`, `airhorn-2.mp3`

### Error Handling

| Scenario | Response |
|----------|----------|
| No sounds in guild | 404 with message "No sounds to export" |
| File missing from disk | Skip file, log warning, include in manifest with `"missing": true` |
| ZIP creation fails | 500 with error details logged |

## Security Considerations

- Authorization via `RequireViewer` policy ensures only authorized users can export
- Temporary files created in isolated scratchpad directory
- Files cleaned up immediately after ZIP creation
- No sensitive data exposed in manifest (user IDs are Discord snowflakes, already public)

## Testing Plan

### Unit Tests
- Export endpoint returns correct content type
- Manifest JSON structure validation
- File naming collision handling

### Integration Tests
- Full export flow with mock sounds
- Empty soundboard handling
- Authorization checks

### Manual Testing
1. Navigate to Guilds/Soundboard for guild with sounds
2. Click Export All button
3. Verify ZIP downloads with correct filename
4. Extract and verify all files present
5. Validate manifest.json contents
6. Test with empty soundboard (button disabled)

## Dependencies

- `System.IO.Compression` (already referenced)
- Existing `ISoundService`, `ISoundFileService`
- Existing `SoundsController` patterns

## Files to Modify

| File | Change |
|------|--------|
| `src/DiscordBot.Bot/Controllers/SoundsController.cs` | Add export endpoint |
| `src/DiscordBot.Bot/Pages/Guilds/Soundboard/Index.cshtml` | Add export button |

## Future Considerations

- Selective export (choose which sounds to include)
- Import from ZIP (restore/migrate soundboard)
- Portal access for guild members (currently admin-only)
- Progress indicator for large soundboards
