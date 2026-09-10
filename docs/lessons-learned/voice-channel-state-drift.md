# Voice channel state drift

**Symptom.** The portal, `/join`, and `/leave` all said the bot was not in a voice channel, but
Discord still showed it sitting in the channel. `/leave` answered "I'm not in a voice channel" and
there was no way to get it out short of restarting the process (or a moderator disconnecting it).

**Cause.** `AudioService.DisconnectInternalAsync` stopped and disposed the `IAudioClient` directly.
In Discord.NET that only tears down the voice websocket and UDP socket. The thing that removes the
bot from the channel is the gateway `VOICE_STATE_UPDATE` with `channel_id: null`, and only
`SocketGuild.DisconnectAudioAsync` (reached through `IVoiceChannel.DisconnectAsync()`) sends it.
Discord.NET does not send it when the audio client dies on its own; a dead voice connection simply
leaves the presence behind. As a bonus, the guild's private `_audioClient` was left pointing at a
disposed client, so the next `ConnectAsync` first tried to stop a disposed object.

**Fix.**

- Leave through `IVoiceChannel.DisconnectAsync()`. Any voice channel of the guild works because it
  delegates to the guild; the app only falls back to `IAudioClient.StopAsync()` when the guild is not
  cached at all.
- `AudioService.ReconcileBotVoiceStateAsync` compares the tracked connection with
  `SocketGuild.CurrentUser.VoiceChannel` and fixes whichever side is wrong: clears tracking when
  Discord says the bot left (kicked, channel deleted, session reset), retargets tracking when a
  moderator moved the bot, and leaves the channel when Discord shows the bot in voice with nothing
  tracked. `VoiceStateHandler` triggers it on the bot's own `UserVoiceStateUpdated` and on `Ready`.
- `/leave` with nothing tracked now still clears a phantom presence instead of saying "not connected".
- `JoinChannelAsync` no longer trusts a tracked connection whose gateway voice state is gone.

**Rules that fell out of it.**

- Never block the gateway thread in a voice-state handler. `ConnectAsync` waits for
  `VOICE_SERVER_UPDATE`, which is dispatched on that same thread, and the reconcile takes the
  per-guild lock the join is holding. The handler offloads with `Task.Run`.
- Reconcile against the live cache (`guild.CurrentUser.VoiceChannel`), not the event payload. The
  bot's own join produces a `null` state event first (Discord.NET disconnects before connecting), and
  by the time the handler gets the lock the payload is stale but the cache is right.
- `SocketGuild.CurrentUser` can be null when the bot member is not cached. Treat that as "unknown"
  and do nothing rather than as "not in voice".
- These paths cannot be unit tested: `DiscordSocketClient`, `SocketGuild`, and `SocketVoiceChannel`
  are sealed and need a live gateway. Tests cover the no-guild and concurrency paths only; verify the
  rest against a real guild.

**Follow-up (the mirror image).** After the fix, the bot left Discord correctly but the portal's
voice panel kept showing the Leave button until a refresh: the panel only reset itself on the
`AudioDisconnected` SignalR event, so a missed or late event left it stale. Two changes close that:
the panel applies the disconnected state itself when the leave request returns 200 (the SignalR
handler is now idempotent on top of that), and the reconcile broadcasts `AudioDisconnected` when it
runs on the bot's own "left voice" event and finds nothing tracked, so clients get a second, truthful
signal. Rule: never make the UI depend on a single fire-and-forget event for state the HTTP response
already confirms.
