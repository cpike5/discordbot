# DAVE (Discord Audio Video Encryption) Implementation

This document describes the DAVE end-to-end encryption implementation used by this bot's forked Discord.Net library, including the MLS protocol flow, audio pipeline integration, native library bindings, and known issues.

## Overview

DAVE is Discord's end-to-end encryption (E2EE) protocol for voice channels, based on the Messaging Layer Security (MLS) standard (RFC 9420). Discord enforced DAVE for all non-stage voice channels on March 2, 2026. Without DAVE support, a bot can neither send nor receive audio in any voice channel.

This bot uses a forked build of Discord.Net rather than the official NuGet packages because the official 3.19.0 release contains a bug where multi-party voice channels fail silently. The fix is described in the "Fork Details" section below.

The DAVE implementation has two distinct layers:

- **Discord.Net.WebSocket** — the managed C# layer that implements the MLS protocol state machine and integrates encryption/decryption into the audio pipeline.
- **libdave** — a native C++ library provided by Discord that implements the actual MLS cryptography and media frame encryption/decryption. The managed code calls into libdave via P/Invoke bindings in the `Discord.Net.Dave` assembly.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                 Bot Application Layer                   │
│         (PlaybackService, AudioService, etc.)           │
└───────────────────────┬─────────────────────────────────┘
                        │ IAudioClient
┌───────────────────────▼─────────────────────────────────┐
│              AudioClient (Discord.Net.WebSocket)         │
│  • WebSocket JSON opcode dispatch                        │
│  • Binary MLS message dispatch (via DaveSessionManager) │
│  • Stream creation and lifecycle management             │
└───────┬───────────────────────────────────┬─────────────┘
        │ Outbound                          │ Inbound
┌───────▼──────────────┐       ┌────────────▼─────────────┐
│   DaveEncryptStream  │       │   DaveDecryptStream       │
│   (per connection)   │       │   (per remote user)       │
└───────┬──────────────┘       └────────────┬─────────────┘
        │                                   │
┌───────▼──────────────────────────────────▼─────────────┐
│            DaveSessionManager                           │
│  • MLS session lifecycle (DaveSession)                  │
│  • One DaveEncryptor (self)                             │
│  • One DaveDecryptor per remote user                    │
│  • _preparedTransitions dictionary                      │
└───────┬──────────────────────────────────┬─────────────┘
        │                                  │
┌───────▼──────────────────────────────────▼─────────────┐
│        Discord.Net.Dave (P/Invoke bindings)             │
│   DaveSession  DaveEncryptor  DaveDecryptor             │
│   DaveKeyRatchet  Dave (factory/utility)                │
└───────┬──────────────────────────────────┬─────────────┘
        │                                  │
┌───────▼──────────────────────────────────▼─────────────┐
│              libdave.so (native C++ library)            │
│   MLS state machine, AES-GCM frame encryption           │
│   Key ratchet derivation, pairwise fingerprints         │
└─────────────────────────────────────────────────────────┘
```

## MLS (Messaging Layer Security) Protocol Flow

MLS is a group key agreement protocol. Each epoch of the MLS group corresponds to a set of shared keys; when membership changes (a user joins or leaves), a new epoch is established with new keys, ensuring forward secrecy and post-compromise security.

### 1. Connection and Protocol Init

When the bot connects to a voice channel, `AudioClient.OnConnectingAsync` sends an `Identify` message that includes the client's maximum supported DAVE protocol version (`_dave.MaxProtocolVersion`). This signals to the voice server that the client supports DAVE.

The voice server responds with a `SessionDescription` (opcode 4) that includes a `DaveProtocolVersion` field. If this version is greater than zero, `HandleDaveProtocolInitAsync` is called to begin the MLS handshake:

```
Bot connects → sends Identify with max DAVE protocol version
Voice server → sends SessionDescription (includes dave_protocol_version)
Bot → calls HandleDaveProtocolInitAsync(protocolVersion)
```

### 2. MLS Session Initialization and Key Package Exchange

`HandleDaveProtocolInitAsync` checks whether the protocol version is active. If it is, it calls `HandlePrepareEpochAsync` with `Dave.MLSNewGroupExpectedEpoch` (value: `1`).

`HandlePrepareEpochAsync` only proceeds when `epoch == 1`. It calls `_session.Initialize(protocolVersion, channelId, selfUserId)` to initialize the native libdave session with the voice channel ID as the MLS group ID and the bot's Discord user ID as the self-identity. After initialization, the session generates an MLS key package and the bot sends it to the voice server:

```
Bot → daveSessionInit(protocolVersion, channelId, selfUserId)
Bot → sends DaveMLSKeyPackage (opcode 26, binary)
```

### 3. External Sender Credentials

The voice server sends a `DaveMLSExternalSender` binary message (opcode 25) containing credentials and a public key for the MLS external sender (the voice server itself). The bot calls `_session.SetExternalSender(payload)` to register these credentials with the native session. This enables the voice server to send MLS proposals without being a member of the group.

### 4. MLS Proposals

The voice server sends `DaveMLSProposals` binary messages (opcode 27) describing membership changes: adds (new users joining) and removes (users leaving). The bot processes these with `_session.ProcessProposals(payload, recognizedUserIds)`.

The `recognizedUserIds` parameter is the set of user IDs whose decryptors are currently tracked — this tells libdave which users are considered legitimate group members.

If `ProcessProposals` returns a non-empty result, the bot generated a commit-welcome message and sends it back:

```
Voice server → DaveMLSProposals (binary, opcode 27)
Bot → _session.ProcessProposals(proposals, knownUserIds)
  If result has data:
Bot → sends DaveMLSCommitWelcome (opcode 28, binary)
```

### 5. Announce Commit Transaction

The voice server sends a `DaveAnnounceCommitTransaction` binary message (opcode 29) containing the chosen commit and a transition ID. The bot processes this commit:

```
Voice server → DaveAnnounceCommitTransaction (binary, opcode 29)
Bot → _session.ProcessCommit(commit)
```

The commit result has three possible outcomes:

- **Ignored** — the commit applies to a different epoch or is otherwise not applicable. The transition ID is removed from `_preparedTransitions` with no further action.
- **Failed** — the commit is invalid. The bot sends `DaveMLSInvalidCommitWelcome` (opcode 31) to signal failure, fetches a fresh key package, and reinitializes the protocol.
- **Succeeded** — the commit is valid. The bot calls `PrepareProtocolTransitionAsync` with the transition ID and the session's current protocol version.

### 6. MLS Welcome (Joining an Existing Group)

When joining a channel that already has active members, the voice server may send a `DaveMLSWelcome` binary message (opcode 30) instead of waiting for a commit cycle. The bot processes this with `_session.ProcessWelcome(payload, recognizedUserIds)`.

If the welcome result is null (invalid or not applicable), the bot sends `DaveMLSInvalidCommitWelcome` and reinitializes. If valid, it calls `PrepareProtocolTransitionAsync`.

### 7. Protocol Transitions: Prepare and Execute

Protocol transitions are identified by a `transitionId` (a `ushort`). The two-phase prepare/execute design allows all clients to agree on a transition point before switching keys.

**Prepare phase** is triggered by two paths:

1. A JSON `DavePrepareTransition` opcode (21) from the voice server, which means the protocol version is being downgraded. `AudioClient` dispatches this directly to `_dave.PrepareProtocolTransitionAsync`.
2. Internally after a successful commit or welcome, `DaveSessionManager` calls `PrepareProtocolTransitionAsync` itself.

For non-init transitions (`transitionId != Dave.InitTransitionId`), `PrepareProtocolTransitionAsync` calls `decryptor.PrepareTransition(session, userId, protocolVersion)` for each tracked remote user, stores the `(transitionId → protocolVersion)` mapping in `_preparedTransitions`, then sends `DaveTransitionReady` (opcode 23) back to the voice server.

For init transitions (`transitionId == 0`), the prepare phase also updates the encryptor's key ratchet and calls `RebuildInputStreamsForDaveAsync` to reconstruct any input streams that were created before the DAVE session was ready.

**Execute phase** is triggered by a JSON `DaveExecuteTransition` opcode (22). `AudioClient` dispatches this to `_dave.ExecuteProtocolTransitionAsync(transitionId)`.

`ExecuteProtocolTransitionAsync` looks up the transition ID in `_preparedTransitions`. If found, it removes the entry and (if the protocol version is `DisabledProtocolVersion`) resets the session and puts the encryptor back into passthrough mode. If not found, it logs "Unexpected transition id: {id}" and returns — this is a known race condition described in the Known Issues section.

```
Voice server → DavePrepareTransition (JSON, opcode 21) or internal trigger
  For each remote user:
    decryptor.PrepareTransition(session, userId, version)
  _preparedTransitions[transitionId] = version
Bot → sends DaveTransitionReady (opcode 23)

Voice server → DaveExecuteTransition (JSON, opcode 22)
Bot → ExecuteProtocolTransitionAsync(transitionId)
  if version == 0: session.Reset(), encryptor.IsInPassthroughMode = true
```

### 8. Epoch Changes on User Join/Leave

The voice server also sends a JSON `DavePrepareEpoc` opcode (24) when a new MLS epoch is about to begin. This carries an `epoch` number and `protocolVersion`. `HandlePrepareEpochAsync` only acts when `epoch == Dave.MLSNewGroupExpectedEpoch` (value 1), which signals a new group is being formed — it reinitializes the session and sends a fresh key package.

When a user joins, the voice server sends a `ClientConnect` opcode (11). If DAVE is active, `AudioClient` calls `_dave.GetOrCreateDecryptor(userId)` for each new user, ensuring a decryptor is ready before any audio arrives.

When a user leaves, the voice server sends `ClientDisconnect` opcode (13), and `_dave.RemoveUser(userId)` is called to dispose and remove their decryptor.

## Voice OpCodes

### JSON Opcodes

| OpCode | Value | Direction | Name | Description |
|--------|-------|-----------|------|-------------|
| `DavePrepareTransition` | 21 | S→C | Prepare Transition | A protocol version downgrade is upcoming; carries `transitionId` and `protocolVersion` |
| `DaveExecuteTransition` | 22 | S→C | Execute Transition | Execute a previously prepared transition; carries `transitionId` |
| `DaveTransitionReady` | 23 | C→S | Transition Ready | Client acknowledges readiness for a transition; carries `transitionId` |
| `DavePrepareEpoc` | 24 | S→C | Prepare Epoch | A DAVE group or version change is upcoming; carries `epoch` and `protocolVersion` |
| `DaveMLSInvalidCommitWelcome` | 31 | C→S | Invalid Commit/Welcome | Flag an invalid commit or welcome; request re-add; carries `transitionId` |

### Binary Opcodes

Binary messages use a 3-byte header: 2-byte big-endian sequence number followed by a 1-byte opcode. The remaining bytes are the payload.

| OpCode | Value | Direction | Name | Description |
|--------|-------|-----------|------|-------------|
| `DaveMLSExternalSender` | 25 | S→C | External Sender | Credential and public key for the MLS external sender (voice server) |
| `DaveMLSKeyPackage` | 26 | C→S | Key Package | MLS key package for this client (sent during group formation) |
| `DaveMLSProposals` | 27 | S→C | Proposals | MLS proposals (member adds/removes) to be processed |
| `DaveMLSCommitWelcome` | 28 | C→S | Commit + Welcome | MLS commit with optional welcome messages (response to proposals) |
| `DaveAnnounceCommitTransaction` | 29 | S→C | Announce Commit | The chosen MLS commit for an upcoming transition; carries 2-byte `transitionId` prefix then commit bytes |
| `DaveMLSWelcome` | 30 | S→C | Welcome | MLS welcome message to join an existing group; carries 2-byte `transitionId` prefix then welcome bytes |

## Key Components

### DaveSessionManager

`DaveSessionManager` is the central coordinator for all DAVE state. It is created fresh on each voice connection (in `SetupLibDave`), so reconnections get a clean slate.

**Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `_session` | `DaveSession` | The native MLS session (one per connection) |
| `Encryptor` | `DaveEncryptor` | The outbound frame encryptor (one per connection) |
| `_decryptors` | `ConcurrentDictionary<ulong, DaveDecryptor>` | One decryptor per remote user ID |
| `_preparedTransitions` | `ConcurrentDictionary<ushort, ushort>` | Maps `transitionId → protocolVersion` for pending transitions |
| `_client` | `AudioClient` | Back-reference to the AudioClient for sending messages and rebuilding streams |

**Key methods:**

| Method | Description |
|--------|-------------|
| `GetOrCreateDecryptor(userId)` | Returns (or creates) the decryptor for a user and calls `PrepareTransition` on it to sync its state |
| `AssignSsrc(ssrc)` | Registers the bot's SSRC with the encryptor so it knows which codec to use |
| `RemoveUser(userId)` | Disposes and removes a user's decryptor when they disconnect |
| `OnBinaryMessageAsync(message)` | Parses the binary header and dispatches to the appropriate MLS handler |
| `HandleDaveProtocolInitAsync(protocolVersion)` | Entry point after `SessionDescription`; either starts MLS group formation or puts encryptor into passthrough mode |
| `HandlePrepareEpochAsync(epoch, protocolVersion)` | Initializes the MLS session when `epoch == 1` and sends the key package |
| `PrepareProtocolTransitionAsync(transitionId, protocolVersion)` | Prepares decryptors for a transition; for init transitions, also updates the encryptor ratchet |
| `ExecuteProtocolTransitionAsync(transitionId)` | Executes a previously prepared transition; for disabled protocol version, resets the session |

**Init transition vs. non-init transitions:**

Transition ID `0` (`Dave.InitTransitionId`) is a sentinel used for the initial key setup on first connection. It bypasses the `_preparedTransitions` dictionary — the prepare and execute steps are combined and the encryptor's ratchet is updated immediately. Non-init transitions (any non-zero ID) go through the two-phase prepare/execute cycle.

### DaveEncryptor

`DaveEncryptor` wraps the native `DAVEEncryptorHandle`. It is used by `DaveEncryptStream` to encrypt each outbound audio frame.

**Key properties:**

| Property | Description |
|----------|-------------|
| `Ratchet` | The `DaveKeyRatchet` derived from the MLS session for the bot's own user ID. Setting this calls `daveEncryptorSetKeyRatchet` on the native handle and disposes the previous ratchet |
| `IsInPassthroughMode` | When `true`, frames are passed through unencrypted. Initially `true` until the init transition completes |
| `ProtocolVersion` | The protocol version the encryptor is currently operating at |

**SSRC-to-codec mapping:** Before encryption can begin, `AssignSsrcToCodec(ssrc, Codec.Opus)` must be called with the bot's SSRC (received in the `Ready` opcode). This tells libdave which codec's header format to use when framing the ciphertext.

**Encryption flow:** For each audio frame, `Encrypt(frame, MediaType.Audio, ssrc, out encrypted, out encryptedLength)` is called. It queries `GetMaxCiphertextByteSize` to rent an appropriately sized buffer from `MemoryPool<byte>.Shared`, then calls the native `daveEncryptorEncrypt`. The actual bytes written are returned via `bytesWritten`.

**Stats:** `GetStats(MediaType.Audio)` returns an `EncryptorStats` struct with counters for pass-through frames, successful encryptions, failures, missing keys, and timing data.

### DaveDecryptor

`DaveDecryptor` wraps the native `DAVEDecryptorHandle`. There is one decryptor per remote user. Decryptors are stored in `DaveSessionManager._decryptors` and are passed to `DaveDecryptStream` instances.

**Key method — PrepareTransition:**

```csharp
public void PrepareTransition(DaveSession session, ulong selfUserId, int? protocolVersion = null)
```

This is called both when a decryptor is first retrieved (`GetOrCreateDecryptor`) and during `PrepareProtocolTransitionAsync`. It:

1. Determines if the protocol version is `DisabledProtocolVersion` (0).
2. Calls `TransitionToPassthroughMode(isDisabled)` — if disabled, frames pass through without decryption.
3. If not disabled, fetches a new key ratchet for the user from the session: `Ratchet = session.GetKeyRatchet(userId)`.

Setting `Ratchet` calls `daveDecryptorTransitionToKeyRatchet` on the native handle, which causes the decryptor to begin accepting frames encrypted with the new epoch's keys.

**Decryption flow:** For each incoming audio frame, `Decrypt(encryptedFrame, MediaType.Audio, out frame, out frameSize)` queries `GetMaxPlaintextByteSize`, rents a buffer, and calls the native `daveDecryptorDecrypt`.

**Stats:** `GetStats(MediaType.Audio)` returns a `DecryptorStats` struct with counters for pass-through frames, successful decryptions, failures, missing keys, and invalid nonces.

### DaveSession (MLS Session)

`DaveSession` wraps the native `DAVESessionHandle`. It manages the MLS group state for the entire voice connection.

**Key methods:**

| Method | Description |
|--------|-------------|
| `Initialize(protocolVersion, groupId, selfUserId)` | Calls `daveSessionInit`; groupId is the Discord channel ID |
| `Reset()` | Calls `daveSessionReset`; used when DAVE is disabled on an active connection |
| `SetExternalSender(payload)` | Registers the voice server's external sender credentials |
| `ProcessProposals(proposals, recognizedUserIds)` | Returns a `DaveAllocatedSpan<byte>` with commit+welcome bytes (empty if nothing to commit) |
| `ProcessCommit(commit)` | Returns a `DaveCommitResult` with `IsFailed`, `IsIgnored`, and roster member information |
| `ProcessWelcome(welcome, recognizedUserIds)` | Returns a `DaveWelcomeResult`; null result means the welcome was not applicable |
| `GetMarshalledKeyPackage()` | Returns the MLS key package to send to the voice server |
| `GetKeyRatchet(userId)` | Derives and returns a `DaveKeyRatchet` for a specific user from the current epoch |
| `GetLastEpochAuthenticator()` | Returns the epoch authenticator bytes (for verification purposes) |
| `GetPairwiseFingerprintAsync(userId)` | Computes a pairwise fingerprint for identity verification |

**MLS failure callback:** When libdave encounters an MLS failure, it invokes the `MLSFailureCallback` function pointer registered at session creation time. The managed wrapper converts this to the `OnMLSFailure` C# event, which `DaveSessionManager` subscribes to and logs at debug severity.

**Memory management:** Most libdave functions that return byte arrays allocate memory internally. The `DaveAllocatedSpan<T>` wrapper holds a pointer to this memory and calls `daveFree` when disposed. Callers use `using` statements to ensure prompt release.

**Thread safety:** All `DaveSession` methods acquire `_lock` (a `System.Threading.Lock`) before calling into native code. This is necessary because MLS state is not thread-safe.

### DaveKeyRatchet

`DaveKeyRatchet` wraps the native `DAVEKeyRatchetHandle`. A ratchet is a cryptographic key derivation primitive: given an initial key, it can produce an ordered sequence of keys, each derived from the previous one. This allows a receiver with a given generation number to catch up to the sender's current generation by stepping the ratchet forward.

Key ratchets are derived from the MLS group state via `DaveSession.GetKeyRatchet(userId)`. Each user has their own ratchet derived from the shared epoch secret. This means:

- Senders use their own ratchet to encrypt.
- Receivers use the sender's ratchet (derived from the same epoch secret) to decrypt.
- Ratchet state advances monotonically, so old frames cannot be decrypted with a new ratchet without stepping it forward.

`DaveKeyRatchet` objects are short-lived: after being assigned to an encryptor or decryptor via `SetKeyRatchet` or `TransitionToKeyRatchet`, the encryptor/decryptor takes a reference, and the `DaveKeyRatchet` C# wrapper can be disposed (which calls `daveKeyRatchetDestroy`). The native encryptor/decryptor holds its own internal reference.

## Audio Pipeline Integration

### Outbound: Bot Speaking

When the bot plays audio, PCM data flows through a chain of `AudioOutStream` objects:

```
PCM data
  └→ OpusEncodeStream     — encodes PCM to Opus
     └→ BufferedWriteStream — buffers audio to smooth packet timing
        └→ DaveEncryptStream — E2EE encrypts the Opus frame
           └→ RTPWriteStream  — adds RTP header (seq, timestamp, SSRC)
              └→ SodiumEncryptStream — XSalsa20-Poly1305 transport encryption
                 └→ OutputStream     — sends over UDP
```

`DaveEncryptStream.WriteAsync` calls `_encryptor.Encrypt(buffer, MediaType.Audio, ssrc)`. If the encryptor is in passthrough mode (DAVE not yet initialized), the frame passes through to the next stream unchanged internally — but note that in the current implementation, the stream would log a warning if encryption fails. When passthrough mode is active at the libdave level, the native library handles pass-through without error.

### Inbound: Remote Users Speaking

When a remote user speaks, UDP packets flow through a chain of `AudioOutStream` objects in reverse:

```
UDP packet
  └→ SodiumDecryptStream — XSalsa20-Poly1305 transport decryption
     └→ RTPReadStream     — strips RTP header, produces (seq, timestamp, payload)
        └→ DaveDecryptStream — E2EE decrypts the Opus frame
           └→ OpusDecodeStream — decodes Opus to PCM
              └→ InputStream    — buffers decoded PCM for reading
```

`DaveDecryptStream.WriteHeader` captures the RTP sequence, timestamp, and missed flag from the RTP layer. On `WriteAsync`, it calls `_decryptor.Decrypt(buffer, MediaType.Audio)`. If decryption succeeds, it forwards the header and decrypted payload to `OpusDecodeStream`.

**Stream rebuild on init transition:** Input streams are created in `CreateInputStreamAsync`, which is called when a remote user first speaks (on `ClientConnect` or `Speaking` opcodes). If the DAVE session is not yet initialized at that moment, the stream is created without the `DaveDecryptStream` layer. When the init transition completes in `PrepareProtocolTransitionAsync`, `RebuildInputStreamsForDaveAsync` is called to destroy all existing input streams and recreate them with the decrypt layer included.

**Per-user decryptors:** Each remote user has their own `DaveDecryptor` instance stored in `_decryptors`. The decryptor is created via `GetOrCreateDecryptor` and is passed directly to the `DaveDecryptStream` constructor. The decryptor's ratchet is updated whenever `PrepareTransition` is called on it.

## Fork Details

### Why the Fork Exists

The official Discord.Net 3.19.0 release contains a bug that causes audio to fail silently in multi-party voice channels (channels with more than one other user). The symptoms are:

1. The bot connects and the MLS session begins initializing.
2. When another user is present, the encryptor never receives a key ratchet.
3. Every attempt to encrypt a frame returns `MissingKeyRatchet`.
4. No audio is ever transmitted.

The root cause is in the original `PrepareProtocolTransitionAsync` implementation: for non-init transitions, the encryptor's ratchet was never updated. Additionally, the original `ExecuteProtocolTransitionAsync` was effectively a no-op for active DAVE protocol versions — it only handled the disabled-protocol case.

The fix (upstream PR #3244, committed to the dev branch) corrects the transition handling so that:

- The encryptor ratchet is updated on init transitions.
- Decryptors are updated with fresh ratchets during prepare.
- `RebuildInputStreamsForDaveAsync` is called after the init transition to add the decrypt layer to streams that were created before DAVE was ready.

### What the Fork Changes vs. Upstream 3.19.0

The fork is built from the upstream dev branch at a commit that includes the PR #3244 fix. Key changes relative to the 3.19.0 release tag:

1. `DaveSessionManager.PrepareProtocolTransitionAsync` — on init transition: updates `Encryptor.Ratchet` from the session, calls `RebuildInputStreamsForDaveAsync`.
2. `AudioClient.RebuildInputStreamsForDaveAsync` — new method that destroys and recreates all input streams to include the `DaveDecryptStream` layer.
3. `DaveDecryptor.PrepareTransition` — new method that combines passthrough-mode update and ratchet assignment.

The fork is versioned `3.19.0-fork` to distinguish it from the official `3.19.0` package.

### How to Rebuild

The fork source is expected at `../Discord.Net` relative to the bot repository root. Use the rebuild script:

```bash
./scripts/rebuild-discord-net.sh
```

This packs all required projects (`Discord.Net.Core`, `Discord.Net.Rest`, `Discord.Net.WebSocket`, `Discord.Net.Commands`, `Discord.Net.Interactions`, `Discord.Net.Dave`) with version suffix `fork` and places the `.nupkg` files into `local-packages/`. After rebuilding, clear the NuGet cache and restore:

```bash
dotnet nuget locals all --clear
dotnet restore src/DiscordBot.Bot/DiscordBot.Bot.csproj
```

To use a custom version suffix (e.g., for testing a different patch):

```bash
./scripts/rebuild-discord-net.sh my-patch
# produces version 3.19.0-my-patch
```

### How to Revert to Upstream

When a new stable Discord.Net release ships with the multi-party DAVE fix:

1. Update the `PackageReference` versions in `DiscordBot.Bot.csproj` and `DiscordBot.Infrastructure.csproj` to the new version.
2. Remove or comment out the `local-packages` source from `nuget.config`.
3. Delete the `local-packages/*.nupkg` files (or leave them; they will be ignored once the feed is removed).
4. Run `dotnet restore`.
5. Remove `COPY local-packages/ local-packages/` from the Dockerfile build stage.
6. Update this document to reflect the upstream version in use.

### NuGet Local Feed

`nuget.config` configures a local feed pointing to the `local-packages/` directory:

```xml
<packageSources>
  <add key="local-packages" value="./local-packages" />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
</packageSources>
```

The local feed takes priority over nuget.org. If a package version exists in both, the local version wins.

### Native libdave Library Requirements

libdave is a prebuilt native C++ shared library provided by Discord. The bot uses version 1.1.1 of the Linux x64 build with BoringSSL:

```
https://github.com/discord/libdave/releases/download/v1.1.1/cpp/libdave-Linux-X64-boringssl.zip
```

**Runtime requirements:**

| Requirement | Minimum Version |
|-------------|-----------------|
| OS | Ubuntu 24.04 (Noble) or equivalent |
| glibc | 2.38 |
| GLIBCXX | 3.4.32 |
| Architecture | x86-64 only |

The Dockerfile uses `mcr.microsoft.com/dotnet/aspnet:8.0-noble` (Ubuntu 24.04) for both build and runtime stages specifically because Debian Bookworm (the default `8.0` tag) ships older glibc/GLIBCXX versions that cannot load libdave. Alpine Linux uses musl instead of glibc and cannot load this binary at all.

The `libdave.so` file is copied to `/usr/lib/x86_64-linux-gnu/libdave.so` in the runtime image. The `DllImport` attribute in `libdave.cs` uses the name `libdave`, which the .NET runtime resolves via the standard Linux shared library search path.

**Enabling DAVE in the bot:**

DAVE is enabled via `DiscordSocketConfig.EnableVoiceDaveEncryption = true` in `DiscordServiceExtensions.cs`:

```csharp
var config = new DiscordSocketConfig
{
    // ...
    EnableVoiceDaveEncryption = true
};
```

This maps to the `LibDaveEnabled` internal property on `DiscordSocketClient`. The behavior of this property is:

| `EnableVoiceDaveEncryption` | libdave available | Behavior |
|------------------------------|-------------------|----------|
| `null` (default) | false | Logs a deprecation warning, continues without DAVE |
| `null` (default) | true | Enables DAVE |
| `true` | true | Enables DAVE |
| `true` | false | Throws `DllNotFoundException` at connection time |
| `false` | any | Disables DAVE unconditionally |

For local development without libdave installed, set `EnableVoiceDaveEncryption = false` to avoid exceptions. In production (Docker), libdave is always present so `true` is appropriate.

## Known Issues

The following bugs exist in the current fork implementation. They are documented here to aid debugging and to inform any future patches.

### Issue 1: Encryptor Ratchet Not Updated on Non-Init Transitions

**Location:** `DaveSessionManager.PrepareProtocolTransitionAsync`

**Description:** When a non-init protocol transition occurs (a user joins or leaves, causing a new MLS epoch), `PrepareProtocolTransitionAsync` updates the ratchets of all decryptors but does not update the encryptor's ratchet. The encryptor continues using the ratchet from the previous epoch.

**Impact:** After the first epoch change, the bot's outbound audio may fail to decrypt for remote users, since they expect frames encrypted with the new epoch's ratchet. In practice this may not cause visible problems if the new epoch's keys are derived such that the decryptors can tolerate the stale generation — but it is technically incorrect behavior that could manifest as audio failures in long-running sessions with frequent membership changes.

**Workaround:** None currently. The bot's primary use case (playing sounds) tends to result in short voice sessions, so this is rarely observed in practice.

### Issue 2: ExecuteProtocolTransitionAsync Is a No-Op for Active Protocols

**Location:** `DaveSessionManager.ExecuteProtocolTransitionAsync`

**Description:** `ExecuteProtocolTransitionAsync` removes the transition from `_preparedTransitions` and then only acts if the protocol version is `DisabledProtocolVersion`. For active protocol versions (any non-zero version), it logs the transition and returns without doing anything.

**Impact:** The intent of the execute phase is to atomically switch all parties to the new epoch's keys. Without any action on execute, the decryptors switch to the new ratchet immediately during the prepare phase (which is the correct behavior), but there is no synchronized cutover. This means some remote frames encrypted with the new epoch might arrive before the execute signal and fail to decrypt.

**Current behavior:** This works acceptably in practice because libdave's decryptor is tolerant of epoch transitions and can hold multiple ratchet generations simultaneously.

### Issue 3: "Unexpected Transition Id" Race Condition

**Location:** `DaveSessionManager.ExecuteProtocolTransitionAsync`

**Description:** When `ExecuteProtocolTransitionAsync` is called with a transition ID not present in `_preparedTransitions`, it logs a warning: "Unexpected transition id: {transitionId}".

**Root cause:** There is a timing window between receiving binary MLS messages (which result in `PrepareProtocolTransitionAsync` adding an entry to `_preparedTransitions`) and receiving the JSON `DaveExecuteTransition` opcode. If the execute message arrives before the prepare phase has completed and added the transition ID, or if the binary commit message is processed after the execute message due to WebSocket message ordering, the transition ID will not be found.

**Impact:** The execute step is skipped. Audio generally continues to work because decryptors were already updated during the prepare phase. However, the log warning may appear frequently in channels with rapid membership changes.

**Observation:** This error appears most often when joining a channel with several members already present, where multiple transitions may be in flight simultaneously.

## Troubleshooting

### "Failed to encrypt dave audio: MissingKeyRatchet"

**Logged by:** `DaveEncryptStream.WriteAsync`

**Meaning:** The encryptor attempted to encrypt a frame but has no key ratchet assigned. This happens when:

1. The MLS init transition has not completed yet (encryptor starts in passthrough mode, so this should not occur if passthrough is working correctly).
2. The bot connected to an empty voice channel. When no other users are present, no MLS group forms and no ratchet is derived. The encryptor remains in passthrough mode (`IsInPassthroughMode = true`), but if passthrough mode was accidentally disabled, encryption will fail.
3. The multi-party DAVE bug from the upstream 3.19.0 release. If you are seeing this error on a fork build, verify that the fork was built correctly from the dev branch.

**Elasticsearch/KQL query:**

```
log.level: "Warning" AND message: "Failed to encrypt dave audio: MissingKeyRatchet"
```

**Resolution:** Check the surrounding log context for the DAVE initialization sequence. You should see "Init dave protocol session", "Initializing dave session", a key package being sent, and ultimately "Preparing to transition to protocol version". If these entries are missing, the MLS handshake is not completing.

### "Failed to decrypt audio packet for {userId}: DecryptionFailure"

**Logged by:** `DaveDecryptStream.WriteAsync`

**Meaning:** The decryptor received a frame it could not decrypt. Possible causes:

1. The decryptor's ratchet is out of sync with the sender's current generation. This can happen if many frames were missed and the ratchet window was exhausted.
2. The remote user's client is not using DAVE (e.g., an older Discord client).
3. The frame was corrupted in transit.
4. An epoch transition is in progress and frames from the new epoch arrived before the decryptor was updated.

**Elasticsearch/KQL query:**

```
log.level: "Warning" AND message: "Failed to decrypt audio packet for *"
```

**To filter by user ID:**

```
log.level: "Warning" AND message: "Failed to decrypt audio packet for 123456789012345678*"
```

**Resolution:** A small number of `DecryptionFailure` events are normal during epoch transitions. Sustained failures for a specific user suggest a ratchet synchronization problem. Check whether the affected user joined mid-session (which would require a new MLS welcome or commit), and whether the epoch transition completed successfully.

### "Failed to decrypt audio packet for {userId}: MissingKeyRatchet"

**Logged by:** `DaveDecryptStream.WriteAsync`

**Meaning:** The decryptor for this user has no ratchet. This happens when audio frames arrive before the MLS session has produced a ratchet for the user — typically during the first few frames after a user joins.

**Elasticsearch/KQL query:**

```
log.level: "Warning" AND message: "Failed to decrypt audio packet for *: MissingKeyRatchet"
```

**Resolution:** This is generally transient and self-resolving. If it persists, check whether the `ClientConnect` opcode was received for this user (which triggers `GetOrCreateDecryptor`) and whether the subsequent MLS commit/welcome cycle completed.

### "Unexpected transition id: {id}"

**Logged by:** `DaveSessionManager.ExecuteProtocolTransitionAsync`

**Meaning:** The execute opcode arrived before the corresponding prepare phase completed. See Known Issue 3 above.

**Elasticsearch/KQL query:**

```
log.level: "Warning" AND message: "Unexpected transition id: *"
```

**Resolution:** This is a known timing issue and is generally benign. If audio is functioning normally, no action is needed. If audio has failed, look for related `MissingKeyRatchet` or `DecryptionFailure` errors to determine whether the transition itself failed.

### "MLS Failure: {source} -> {reason}"

**Logged by:** `DaveSessionManager` constructor (MLS failure callback)

**Meaning:** The native libdave library reported an MLS protocol failure. The `source` field identifies the libdave internal module, and `reason` provides a human-readable description.

Common reasons include:
- Mismatched group IDs (bot's channel ID does not match the group the voice server is using).
- Invalid key packages (malformed or rejected by the group).
- Epoch validation failures (received a commit for an epoch the client is not on).

**Elasticsearch/KQL query:**

```
log.level: "Debug" AND message: "MLS Failure: *"
```

**Note:** This is logged at debug severity. In production, you must have debug logging enabled for the `Dave #N` logger to see it.

**Resolution:** After an MLS failure, the bot should receive a new `DavePrepareEpoc` or `DaveMLSProposals` message from the voice server to reinitiate the handshake. If failures are persistent, check that the bot's Discord user ID and the channel ID are being passed correctly to `_session.Initialize`.

## See Also

- [Audio Dependencies](audio-dependencies.md) — FFmpeg, libsodium, libopus setup
- [Voice Capability System](voice-capability-system.md) — Bot voice connection management
- [Docker Deployment](docker-deployment.md) — Container configuration including libdave installation
