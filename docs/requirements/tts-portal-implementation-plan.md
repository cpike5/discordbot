# TTS Portal Implementation Plan

**Feature**: Web-based TTS portal for guild members
**Status**: Approved by Systems Architect
**Estimated Effort**: 9-13 hours (1-2 sprints)
**Risk Level**: LOW
**Dependencies**: Existing TTS feature, Azure Speech SDK, Discord OAuth

## Overview

Implementation of a web-based text-to-speech portal allowing authenticated guild members to send TTS messages to voice channels without using Discord slash commands. Follows the established pattern from the Soundboard portal.

## Architecture Summary

- **Pattern**: Razor Pages with AJAX API endpoints
- **Authorization**: `PortalGuildMember` policy (Discord OAuth + membership check)
- **Services**: Reuses `ITtsService`, `IAudioService`, `IPlaybackService`
- **Database**: No schema changes (uses existing `GuildTtsSettings`, `TtsMessages`)
- **Frontend**: Tailwind CSS, vanilla JavaScript with status polling

## Implementation Phases

### Phase 1: Backend Foundation (2-3 hours)

**Files to Create**:
1. `src/DiscordBot.Bot/Controllers/PortalTtsController.cs`
2. `src/DiscordBot.Core/DTOs/Portal/SendTtsRequest.dto.cs`
3. `src/DiscordBot.Core/DTOs/Portal/TtsStatusResponse.dto.cs`
4. `src/DiscordBot.Core/DTOs/Portal/VoiceChannelInfo.dto.cs`

**API Endpoints**:

```csharp
// PortalTtsController.cs
[ApiController]
[Route("api/portal/tts")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalTtsController : ControllerBase
{
    // GET api/portal/tts/{guildId}/status
    [HttpGet("{guildId:long}/status")]
    public async Task<ActionResult<TtsStatusResponse>> GetStatus(ulong guildId)

    // POST api/portal/tts/{guildId}/send
    [HttpPost("{guildId:long}/send")]
    public async Task<IActionResult> SendTts(ulong guildId, [FromBody] SendTtsRequest request)

    // POST api/portal/tts/{guildId}/channel
    [HttpPost("{guildId:long}/channel")]
    public async Task<IActionResult> JoinChannel(ulong guildId, [FromBody] JoinChannelRequest request)

    // DELETE api/portal/tts/{guildId}/channel
    [HttpDelete("{guildId:long}/channel")]
    public async Task<IActionResult> LeaveChannel(ulong guildId)
}
```

**Rate Limiting Implementation**:
```csharp
// In SendTts method
var userId = User.GetDiscordUserId();
var settings = await _ttsSettingsRepository.GetByGuildIdAsync(guildId);

if (!await _rateLimitService.CheckRateLimitAsync(
    $"tts:{guildId}:{userId}",
    settings.RateLimitPerMinute,
    TimeSpan.FromMinutes(1)))
{
    return StatusCode(429, new ApiErrorDto {
        Message = "Rate limit exceeded",
        Detail = $"Maximum {settings.RateLimitPerMinute} TTS messages per minute."
    });
}
```

**Current Message Tracking**:
```csharp
// In-memory cache for "Now Playing" feature
private static readonly ConcurrentDictionary<ulong, string> _currentMessages = new();

// After successful synthesis
_currentMessages.AddOrUpdate(guildId, message.Substring(0, Math.Min(50, message.Length)),
    (k, v) => message.Substring(0, Math.Min(50, message.Length)));
```

**Tests**:
- `tests/DiscordBot.Tests/Controllers/PortalTtsControllerTests.cs`
  - Test rate limiting
  - Test validation (message length, empty message)
  - Test authorization (guild membership)
  - Test TTS disabled guild returns 400

---

### Phase 2: Razor Page (2-3 hours)

**Files to Create**:
1. `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml`
2. `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml.cs`
3. `src/DiscordBot.Core/ViewModels/Portal/PortalTtsViewModel.cs`

**PageModel Structure**:

```csharp
[AllowAnonymous] // Landing page accessible to unauthenticated
public class IndexModel : PageModel
{
    public bool IsAuthenticated { get; set; }
    public bool IsAuthorized { get; set; }
    public string LoginUrl { get; set; }
    public ulong GuildId { get; set; }
    public string GuildName { get; set; }
    public string? GuildIconUrl { get; set; }
    public bool IsOnline { get; set; }

    // Authenticated-only properties
    public List<VoiceChannelInfo> VoiceChannels { get; set; }
    public ulong? CurrentChannelId { get; set; }
    public bool IsConnected { get; set; }
    public List<VoiceInfo> AvailableVoices { get; set; }
    public GuildTtsSettings Settings { get; set; }

    public async Task<IActionResult> OnGetAsync(ulong guildId)
    {
        GuildId = guildId;

        // Check if TTS is enabled
        var settings = await _ttsSettingsRepository.GetByGuildIdAsync(guildId);
        if (settings == null || !settings.TtsEnabled)
        {
            return NotFound("TTS is not enabled for this server.");
        }

        // Load guild info from Discord API
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            return NotFound("Server not found.");
        }

        GuildName = guild.Name;
        GuildIconUrl = guild.IconUrl;
        IsOnline = true;

        // Check authentication
        IsAuthenticated = User.Identity?.IsAuthenticated ?? false;

        if (!IsAuthenticated)
        {
            LoginUrl = $"/Account/Login?returnUrl={HttpUtility.UrlEncode($"/Portal/TTS/{guildId}")}";
            return Page();
        }

        // Check guild membership
        var discordUserId = User.GetDiscordUserId();
        var guildUser = guild.GetUser(discordUserId);

        if (guildUser == null)
        {
            IsAuthorized = false;
            return Page(); // Show "not a member" message
        }

        IsAuthorized = true;

        // Load authenticated data
        Settings = settings;
        VoiceChannels = guild.VoiceChannels
            .Select(vc => new VoiceChannelInfo { Id = vc.Id, Name = vc.Name })
            .ToList();

        var currentChannel = await _audioService.GetConnectedChannelAsync(guildId);
        CurrentChannelId = currentChannel?.Id;
        IsConnected = currentChannel != null;

        AvailableVoices = await _ttsService.GetAvailableVoicesAsync();

        return Page();
    }
}
```

**View Structure** (Two States):

```razor
@page "/Portal/TTS/{guildId:long}"
@model DiscordBot.Bot.Pages.Portal.TTS.IndexModel

@if (!Model.IsAuthenticated)
{
    <!-- Landing Page -->
    <div class="portal-landing">
        <div class="guild-info">
            @if (!string.IsNullOrEmpty(Model.GuildIconUrl))
            {
                <img src="@Model.GuildIconUrl" alt="@Model.GuildName" class="guild-icon" />
            }
            <h1>@Model.GuildName TTS Portal</h1>
            <p>Send text-to-speech messages to voice channels</p>
        </div>

        <a href="@Model.LoginUrl" class="btn-primary">
            <svg><!-- Discord icon --></svg>
            Sign in with Discord
        </a>
    </div>
}
else if (!Model.IsAuthorized)
{
    <!-- Not a member -->
    <div class="alert alert-error">
        You must be a member of @Model.GuildName to access this portal.
    </div>
}
else
{
    <!-- Full TTS Portal -->
    <div class="portal-container">
        <!-- Main Content: TTS Form -->
        <main class="portal-main">
            <h1>Text-to-Speech</h1>

            <form id="ttsForm">
                <div class="form-group">
                    <label for="messageInput">Message</label>
                    <textarea id="messageInput"
                              maxlength="@Model.Settings.MaxMessageLength"
                              rows="6"
                              aria-label="Text to speech message input"></textarea>
                    <div id="charCounter" aria-live="polite">0/@Model.Settings.MaxMessageLength</div>
                </div>

                <button type="submit" id="sendBtn" class="btn-primary">
                    Send to Channel
                </button>
            </form>
        </main>

        <!-- Sidebar: Controls & Status -->
        <aside class="portal-sidebar">
            <!-- Voice Channel Selector -->
            <div class="control-group">
                <label>Voice Channel</label>
                <select id="channelSelect">
                    <option value="">Select a channel...</option>
                    @foreach (var channel in Model.VoiceChannels)
                    {
                        <option value="@channel.Id"
                                selected="@(channel.Id == Model.CurrentChannelId)">
                            @channel.Name
                        </option>
                    }
                </select>
                <div id="connectionStatus" class="status-indicator"></div>
            </div>

            <!-- Voice Settings -->
            <div class="control-group">
                <label>Voice</label>
                <select id="voiceSelect">
                    @foreach (var voiceGroup in Model.AvailableVoices.GroupBy(v => v.Locale))
                    {
                        <optgroup label="@voiceGroup.Key">
                            @foreach (var voice in voiceGroup)
                            {
                                <option value="@voice.ShortName"
                                        selected="@(voice.ShortName == Model.Settings.DefaultVoice)">
                                    @voice.DisplayName
                                </option>
                            }
                        </optgroup>
                    }
                </select>
            </div>

            <div class="control-group">
                <label>Speed: <span id="speedValue">@Model.Settings.DefaultSpeed</span></label>
                <input type="range" id="speedSlider"
                       min="0.5" max="2.0" step="0.1"
                       value="@Model.Settings.DefaultSpeed" />
            </div>

            <div class="control-group">
                <label>Pitch: <span id="pitchValue">@Model.Settings.DefaultPitch</span></label>
                <input type="range" id="pitchSlider"
                       min="0.5" max="2.0" step="0.1"
                       value="@Model.Settings.DefaultPitch" />
            </div>

            <!-- Now Playing -->
            <div id="nowPlayingPanel" class="now-playing" style="display: none;">
                <h3>Now Playing</h3>
                <p id="nowPlayingMessage"></p>
                <button id="stopBtn" class="btn-danger btn-sm">Stop</button>
            </div>
        </aside>
    </div>
}

@section Scripts {
    <script src="~/js/portal-tts.js"></script>
}
```

---

### Phase 3: Frontend Interactions (3-4 hours)

**File to Create**:
- `src/DiscordBot.Bot/wwwroot/js/portal-tts.js`

**Features to Implement**:

#### 1. Character Counter
```javascript
const maxLength = parseInt(document.getElementById('messageInput').getAttribute('maxlength'));
const textarea = document.getElementById('messageInput');
const counter = document.getElementById('charCounter');

textarea.addEventListener('input', function() {
    const length = this.value.length;
    counter.textContent = `${length}/${maxLength}`;

    if (length >= maxLength * 0.9) {
        counter.classList.add('text-warning');
    } else {
        counter.classList.remove('text-warning');
    }
});
```

#### 2. AJAX Form Submission
```javascript
document.getElementById('ttsForm').addEventListener('submit', async function(e) {
    e.preventDefault();

    const message = textarea.value.trim();
    if (!message) {
        PortalToast.error('Please enter a message');
        return;
    }

    const sendBtn = document.getElementById('sendBtn');
    sendBtn.disabled = true;
    sendBtn.innerHTML = '<svg class="animate-spin">...</svg> Sending...';

    try {
        const response = await fetch(`/api/portal/tts/${window.guildId}/send`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                message: message,
                voice: document.getElementById('voiceSelect').value,
                speed: parseFloat(document.getElementById('speedSlider').value),
                pitch: parseFloat(document.getElementById('pitchSlider').value)
            })
        });

        if (response.ok) {
            PortalToast.success('TTS message sent successfully');
            textarea.value = ''; // Clear textarea
            counter.textContent = `0/${maxLength}`; // Reset counter
        } else if (response.status === 429) {
            const error = await response.json();
            PortalToast.error(error.detail || 'Rate limit exceeded');
        } else {
            PortalToast.error('Failed to send TTS message');
        }
    } catch (error) {
        PortalToast.error('Network error: ' + error.message);
    } finally {
        sendBtn.disabled = false;
        sendBtn.innerHTML = 'Send to Channel';
    }
});
```

#### 3. Status Polling
```javascript
let isConnected = false;

async function pollStatus() {
    try {
        const response = await fetch(`/api/portal/tts/${window.guildId}/status`);
        if (!response.ok) return;

        const data = await response.json();

        // Update connection state
        if (data.isConnected !== isConnected) {
            isConnected = data.isConnected;
            updateConnectionUI(isConnected);
        }

        // Update now playing
        updateNowPlayingUI(data.isPlaying ? data.currentMessage : null);

    } catch (error) {
        console.error('Status poll failed:', error);
    }
}

function updateConnectionUI(connected) {
    const status = document.getElementById('connectionStatus');
    const sendBtn = document.getElementById('sendBtn');

    if (connected) {
        status.textContent = 'Connected';
        status.className = 'status-indicator status-online';
        sendBtn.disabled = false;
    } else {
        status.textContent = 'Not connected';
        status.className = 'status-indicator status-offline';
        sendBtn.disabled = true;
    }
}

function updateNowPlayingUI(message) {
    const panel = document.getElementById('nowPlayingPanel');
    const messageEl = document.getElementById('nowPlayingMessage');

    if (message) {
        messageEl.textContent = message; // Use textContent to prevent XSS
        panel.style.display = 'block';
    } else {
        panel.style.display = 'none';
    }
}

// Poll every 3 seconds
setInterval(pollStatus, 3000);
pollStatus(); // Initial poll
```

#### 4. Toast Notification System
```javascript
const PortalToast = {
    show(message, type) {
        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.textContent = message;
        document.body.appendChild(toast);

        setTimeout(() => toast.classList.add('show'), 100);
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    },
    success(msg) { this.show(msg, 'success'); },
    error(msg) { this.show(msg, 'error'); },
    warning(msg) { this.show(msg, 'warning'); },
    info(msg) { this.show(msg, 'info'); }
};
```

#### 5. Voice Channel Selection
```javascript
document.getElementById('channelSelect').addEventListener('change', async function() {
    const channelId = this.value;

    if (!channelId) {
        // Leave channel
        try {
            await fetch(`/api/portal/tts/${window.guildId}/channel`, {
                method: 'DELETE'
            });
            PortalToast.info('Left voice channel');
        } catch (error) {
            PortalToast.error('Failed to leave channel');
        }
        return;
    }

    // Join channel
    try {
        const response = await fetch(`/api/portal/tts/${window.guildId}/channel`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ channelId })
        });

        if (response.ok) {
            PortalToast.success('Joined voice channel');
        } else {
            PortalToast.error('Failed to join channel');
        }
    } catch (error) {
        PortalToast.error('Network error: ' + error.message);
    }
});
```

#### 6. Stop Button
```javascript
document.getElementById('stopBtn').addEventListener('click', async function() {
    try {
        const response = await fetch(`/api/portal/tts/${window.guildId}/stop`, {
            method: 'POST'
        });

        if (response.ok) {
            PortalToast.info('Playback stopped');
        }
    } catch (error) {
        PortalToast.error('Failed to stop playback');
    }
});
```

---

### Phase 4: Testing & Polish (2-3 hours)

**Unit Tests**:

```csharp
// tests/DiscordBot.Tests/Controllers/PortalTtsControllerTests.cs
public class PortalTtsControllerTests : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task SendTts_ValidMessage_Returns200()

    [Fact]
    public async Task SendTts_MessageTooLong_Returns400()

    [Fact]
    public async Task SendTts_RateLimitExceeded_Returns429()

    [Fact]
    public async Task SendTts_NotConnectedToChannel_Returns400()

    [Fact]
    public async Task GetStatus_NotAuthenticated_Returns401()

    [Fact]
    public async Task GetStatus_NotGuildMember_Returns403()
}
```

**Integration Tests**:

```csharp
// tests/DiscordBot.Tests/Integration/PortalTtsIntegrationTests.cs
public class PortalTtsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task FullFlow_OAuthToTtsSend_Success()
    {
        // 1. Navigate to portal (unauthenticated)
        // 2. Click login, complete OAuth
        // 3. Verify guild membership
        // 4. Load TTS portal
        // 5. Send TTS message
        // 6. Verify TtsMessages table entry
    }
}
```

**Manual Testing Checklist**:
- [ ] Unauthenticated user sees landing page with guild name/icon
- [ ] Discord OAuth redirects back to portal with returnUrl
- [ ] Non-member shows "not a member" error
- [ ] Member sees full TTS portal interface
- [ ] Character counter updates in real-time (0/500 → 50/500)
- [ ] Character counter shows warning at 450+ chars
- [ ] Send button disabled when textarea empty
- [ ] Send button disabled when not connected to voice channel
- [ ] Send button shows loading state during synthesis
- [ ] Toast notification appears on success
- [ ] Textarea clears on successful send
- [ ] Voice/speed/pitch sliders remain at selected values after send
- [ ] Rate limiting triggers after N messages (shows 429 error toast)
- [ ] Status polling updates connection state every 3 seconds
- [ ] Now Playing shows current message text (max 50 chars)
- [ ] Stop button stops current playback
- [ ] Voice dropdown shows categorized voices (English US, English UK, etc.)
- [ ] Mobile: sidebar stacks on top of main content
- [ ] Mobile: sliders are touch-friendly (44x44 minimum)
- [ ] Keyboard: Tab navigation works through all controls
- [ ] Keyboard: Enter in textarea submits form
- [ ] Screen reader: ARIA labels present for textarea and counter

---

## Database Considerations

**No schema changes required**. The portal uses existing tables:

### GuildTtsSettings (Configuration)
- `TtsEnabled` - Feature toggle (check in PageModel)
- `MaxMessageLength` - Character limit for textarea
- `RateLimitPerMinute` - Rate limit enforcement
- `DefaultVoice`, `DefaultSpeed`, `DefaultPitch` - Default values

### TtsMessages (Logging)
Each successful TTS send creates an entry:
```csharp
var ttsMessage = new TtsMessage
{
    GuildId = guildId,
    UserId = User.GetDiscordUserId(),
    Username = User.GetDiscordUsername(),
    Message = request.Message,
    Voice = request.Voice,
    DurationSeconds = duration,
    CreatedAt = DateTime.UtcNow
};
await _ttsMessagesRepository.CreateAsync(ttsMessage);
```

**Performance**: Add index for analytics queries:
```sql
CREATE INDEX IX_TtsMessages_GuildId_CreatedAt
ON TtsMessages (GuildId, CreatedAt DESC);
```

---

## CSS Styling

**New CSS file** (optional, can use inline Tailwind):
- `src/DiscordBot.Bot/wwwroot/css/portal-tts.css`

**Reuse from Soundboard**:
- `.portal-container` - Grid layout (main + sidebar)
- `.portal-main` - Main content area
- `.portal-sidebar` - Right sidebar controls
- `.control-group` - Grouped form controls
- `.now-playing` - Now Playing panel styling
- `.toast` - Toast notification styling

**New Styles**:
```css
/* Character counter */
#charCounter {
    text-align: right;
    font-size: 0.875rem;
    color: var(--text-secondary);
    margin-top: 0.25rem;
}

#charCounter.text-warning {
    color: var(--warning);
}

/* TTS textarea */
#messageInput {
    width: 100%;
    padding: 0.75rem;
    border: 1px solid var(--border);
    border-radius: 0.375rem;
    font-family: inherit;
    resize: vertical;
}

/* Voice dropdown with groups */
#voiceSelect optgroup {
    font-weight: 600;
    font-style: normal;
}

#voiceSelect option {
    padding-left: 1rem;
}
```

---

## Documentation Updates

### 1. Update `docs/articles/tts-support.md`

Add new section:

```markdown
## TTS Member Portal

Guild members can access a web-based TTS portal to send text-to-speech messages without using Discord slash commands.

**Portal URL**: `/Portal/TTS/{guildId}`

### Features

- **Authentication**: Discord OAuth required (guild membership verified)
- **Voice Channel Selection**: Join/leave voice channels via dropdown
- **Message Input**: Textarea with real-time character counter (max 500 chars)
- **Voice Customization**: Select voice, adjust speed (0.5-2.0x), adjust pitch (0.5-2.0x)
- **Now Playing**: Shows current TTS message being played
- **Rate Limiting**: Enforced per-user based on `GuildTtsSettings.RateLimitPerMinute`
- **Mobile Responsive**: Sidebar stacks on mobile devices

### Access Control

- Unauthenticated users see landing page with "Sign in with Discord" button
- Non-members of the guild see access denied message
- Only guild members can access the full portal

### Configuration

Portal respects guild TTS settings:
- `TtsEnabled` - Portal returns 404 if TTS is disabled
- `MaxMessageLength` - Character limit for textarea
- `RateLimitPerMinute` - Rate limiting per user
- `DefaultVoice`, `DefaultSpeed`, `DefaultPitch` - Initial slider values
```

### 2. Update `CLAUDE.md`

Add route to UI Page Routes table:

```markdown
| TTS Portal | `/Portal/TTS/{guildId:long}` | TTS message composer for guild members (OAuth required) |
```

---

## Security Considerations

### 1. XSS Prevention

**Critical**: Now Playing displays user-submitted text.

```javascript
// WRONG - XSS vulnerability
document.getElementById('nowPlayingMessage').innerHTML = data.currentMessage;

// CORRECT - Safe text rendering
document.getElementById('nowPlayingMessage').textContent = data.currentMessage;
```

### 2. Rate Limiting

User-level rate limiting prevents abuse:
```csharp
var userId = User.GetDiscordUserId();
var key = $"tts:{guildId}:{userId}";
await _rateLimitService.CheckRateLimitAsync(key, limit, TimeSpan.FromMinutes(1));
```

### 3. Input Validation

```csharp
// In SendTtsRequest DTO
public class SendTtsRequest
{
    [Required]
    [MaxLength(500)]
    public string Message { get; set; }

    [Required]
    public string Voice { get; set; }

    [Range(0.5, 2.0)]
    public double Speed { get; set; } = 1.0;

    [Range(0.5, 2.0)]
    public double Pitch { get; set; } = 1.0;
}
```

### 4. Authorization Checks

Every API endpoint validates:
1. User is authenticated (OAuth)
2. User has linked Discord account
3. User is member of the guild
4. TTS is enabled for the guild

---

## Performance Optimization

### 1. Status Polling Efficiency

3-second polling interval with lightweight response:
```csharp
[HttpGet("{guildId:long}/status")]
public async Task<ActionResult<TtsStatusResponse>> GetStatus(ulong guildId)
{
    // No database queries - only in-memory checks
    var isConnected = await _audioService.IsConnectedAsync(guildId);
    var isPlaying = _playbackService.IsPlaying(guildId);
    var currentMessage = isPlaying ? _currentMessages.GetValueOrDefault(guildId) : null;

    return Ok(new TtsStatusResponse
    {
        IsConnected = isConnected,
        IsPlaying = isPlaying,
        CurrentMessage = currentMessage
    });
}
```

### 2. Voice List Caching

Azure Speech voice list rarely changes - cache for 24 hours:
```csharp
private static readonly MemoryCache _voiceCache = new MemoryCache(new MemoryCacheOptions());

public async Task<List<VoiceInfo>> GetAvailableVoicesAsync()
{
    const string cacheKey = "azure_speech_voices";

    if (_voiceCache.TryGetValue(cacheKey, out List<VoiceInfo> voices))
    {
        return voices;
    }

    voices = await FetchVoicesFromAzureAsync();
    _voiceCache.Set(cacheKey, voices, TimeSpan.FromHours(24));
    return voices;
}
```

### 3. Database Index

Add index for future analytics queries:
```sql
CREATE INDEX IX_TtsMessages_GuildId_CreatedAt
ON TtsMessages (GuildId, CreatedAt DESC);
```

---

## Future Enhancements (Out of Scope)

Document for future consideration:

1. **Preview Button**: Client-side audio preview before sending to channel
2. **Message History**: Show last 10 TTS messages with replay button
3. **Voice Presets**: Save favorite voice/speed/pitch combinations per user
4. **Queue Management**: Show queued messages when multiple users send simultaneously
5. **SignalR Real-time**: Replace polling with real-time updates via SignalR hub
6. **Message Templates**: Save frequently used phrases ("Good morning", "Break time", etc.)
7. **SSML Support**: Advanced users can write custom SSML for prosody control

---

## Rollout Plan

### Development Environment
1. Implement phases 1-4
2. Test with single guild (test server)
3. Verify rate limiting works correctly
4. Test on mobile devices

### Staging Environment
1. Deploy to staging
2. Test with multiple guilds
3. Load testing (50+ concurrent users)
4. Monitor Azure Speech API usage and costs

### Production Deployment
1. Deploy backend + frontend together (single PR)
2. Announce feature in Discord guilds
3. Monitor error logs (Seq/Elasticsearch)
4. Monitor Azure Speech API costs
5. Gather user feedback for future enhancements

---

## Success Metrics

Track the following after launch:

1. **Usage**: TTS messages sent via portal vs. slash command
2. **Adoption**: Number of unique users accessing portal per guild
3. **Errors**: 429 rate limit responses (indicates need for higher limits)
4. **Performance**: Average API response time for `/send` endpoint
5. **Cost**: Azure Speech API usage increase (monitor billing)

---

## Approval Signatures

- [x] **Systems Architect**: Approved (see architectural review)
- [ ] **Product Owner**: Pending
- [ ] **Tech Lead**: Pending

---

## Related Documents

- [TTS Portal Requirements](tts-portal.md)
- [TTS Portal Architectural Review](../temp/tts-portal-architectural-review.md)
- [TTS Support Documentation](../articles/tts-support.md)
- [Soundboard Documentation](../articles/soundboard.md) (similar pattern)
- [Form Implementation Standards](../articles/form-implementation-standards.md)
