using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Seeds one row per personal-data table that the user purge and export cover since the H10 fix
/// (preferences, sound favourites, TTS presets and history, VOX history, audio playback, DM
/// conversation and notes, activity events and snapshots, feature requests, wallets and the
/// ledger, notifications and the account activity log), for one Discord user and, optionally,
/// the web account linked to it. Seeding two users lets a test prove the other user's rows survive.
/// </summary>
public static class PersonalDataSeeder
{
    /// <summary>Seeds the guild, sound and currency the per-user rows point at. Call once per database.</summary>
    public static async Task<(Guild Guild, Sound Sound, Currency Currency)> SeedSharedAsync(BotDbContext context, ulong guildId)
    {
        var guild = new Guild { Id = guildId, Name = "Inventory Guild", JoinedAt = DateTime.UtcNow };
        var sound = new Sound
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            Name = "airhorn",
            FileName = "airhorn.mp3",
            FileSizeBytes = 1024,
            DurationSeconds = 1.5,
            UploadedAt = DateTime.UtcNow
        };
        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Scope = CurrencyScope.Guild,
            GuildId = guildId,
            Name = "Coins",
            Symbol = "C",
            CreatedById = 1UL,
            CreatedAt = DateTime.UtcNow
        };

        context.Guilds.Add(guild);
        context.Sounds.Add(sound);
        context.Currencies.Add(currency);
        await context.SaveChangesAsync();
        return (guild, sound, currency);
    }

    /// <summary>
    /// Seeds a <see cref="User"/> and one row in each newly covered table for it. When
    /// <paramref name="applicationUserId"/> is given, also seeds a linked web account with a
    /// notification and an account activity log row it acted in.
    /// </summary>
    public static async Task SeedUserAsync(
        BotDbContext context,
        ulong discordUserId,
        ulong guildId,
        Guid soundId,
        Guid currencyId,
        string? applicationUserId = null)
    {
        var now = DateTime.UtcNow;
        var tag = discordUserId.ToString();

        context.Users.Add(new User { Id = discordUserId, Username = $"user{tag}" });

        context.UserPreferences.Add(new UserPreference { GuildId = guildId, UserId = discordUserId, Key = "tts.voice", Value = "en-US-JennyNeural", UpdatedAt = now });
        context.UserSoundFavorites.Add(new UserSoundFavorite { GuildId = guildId, UserId = discordUserId, SoundId = soundId, FavoritedAt = now });
        context.UserTtsPresets.Add(new UserTtsPreset { UserId = discordUserId, Name = $"preset {tag}", VoiceName = "en-US-JennyNeural", CreatedAt = now });
        context.TtsMessageHistory.Add(new TtsMessageHistory { GuildId = guildId, UserId = discordUserId, Message = $"tts {tag}", VoiceName = "en-US-JennyNeural", Speed = 1m, Pitch = 1m, PlayedAt = now });
        context.VoxMessageHistory.Add(new VoxMessageHistory { GuildId = guildId, UserId = discordUserId, Message = $"vox {tag}", ClipGroup = "vox", PlayedAt = now });
        context.AudioPlaybackLogs.Add(new AudioPlaybackLog { GuildId = guildId, UserId = discordUserId, FeatureType = AudioFeatureType.Soundboard, ContentName = "airhorn", PlayedAt = now });
        context.DmConversationMessages.Add(new DmConversationMessage { UserId = discordUserId, Role = "user", Content = $"dm {tag}", Timestamp = now });
        context.DmAssistantNotes.Add(new DmAssistantNote { UserId = discordUserId, Tag = "pref", Content = $"note {tag}", CreatedAt = now, UpdatedAt = now });
        context.UserActivityEvents.Add(new UserActivityEvent { UserId = discordUserId, GuildId = guildId, ChannelId = 42UL, Timestamp = now, LoggedAt = now, EventType = ActivityEventType.Message });
        context.MemberActivitySnapshots.Add(new MemberActivitySnapshot { GuildId = guildId, UserId = discordUserId, PeriodStart = now.Date, Granularity = SnapshotGranularity.Daily, MessageCount = 3, CreatedAt = now });
        context.FeatureRequests.Add(new FeatureRequest { Id = Guid.NewGuid(), GuildId = guildId, SubmittedByUserId = discordUserId, Title = $"request {tag}", Description = "please", Status = FeatureRequestStatus.Submitted, CreatedAt = now, UpdatedAt = now });
        context.FeatureRequestRejections.Add(new FeatureRequestRejection { Id = Guid.NewGuid(), GuildId = guildId, UserId = discordUserId, RejectionReason = "off topic", CreatedAt = now });

        var wallet = new Wallet { Id = Guid.NewGuid(), CurrencyId = currencyId, UserId = discordUserId, CachedBalance = 50, CreatedAt = now };
        context.Wallets.Add(wallet);
        context.LedgerTransactions.Add(new LedgerTransaction
        {
            WalletId = wallet.Id,
            Type = LedgerTransactionType.Mint,
            Source = LedgerSource.Manual,
            Amount = 50,
            BalanceAfter = 50,
            IdempotencyKey = $"seed-{tag}",
            CreatedAt = now
        });

        if (applicationUserId != null)
        {
            context.Set<ApplicationUser>().Add(new ApplicationUser
            {
                Id = applicationUserId,
                UserName = $"web{tag}",
                NormalizedUserName = $"WEB{tag}",
                Email = $"web{tag}@example.com",
                NormalizedEmail = $"WEB{tag}@EXAMPLE.COM",
                DiscordUserId = discordUserId
            });
            context.UserNotifications.Add(new UserNotification { Id = Guid.NewGuid(), UserId = applicationUserId, Title = "Hello", Message = $"notification {tag}", CreatedAt = now });
            // The account acted in the portal (as a former admin would have): a Restrict FK on the actor.
            context.UserActivityLogs.Add(new UserActivityLog { Id = Guid.NewGuid(), ActorUserId = applicationUserId, Action = UserActivityAction.UserUpdated, Details = "{}", Timestamp = now });
        }

        await context.SaveChangesAsync();
    }
}
