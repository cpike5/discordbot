using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.Vox;
using DiscordBot.Infrastructure.Services.Vox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering voice, soundboard, and TTS services.
/// </summary>
public static class VoiceServiceExtensions
{
    /// <summary>
    /// Adds all voice-related services to the service collection.
    /// This is a composite method that calls AddVoiceCore, AddSoundboard, AddTts, and AddVox.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVoiceSupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddVoiceCore(configuration);
        services.AddSoundboard(configuration);
        services.AddTts(configuration);
        services.AddVox(configuration);

        return services;
    }

    /// <summary>
    /// Adds core voice channel services including audio playback and auto-leave functionality.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVoiceCore(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<VoiceChannelOptions>(
            configuration.GetSection(VoiceChannelOptions.SectionName));

        // Core audio services (singleton for connection management)
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<IPlaybackService, PlaybackService>();

        // Background service for auto-leave
        services.AddHostedService<VoiceAutoLeaveService>();

        return services;
    }

    /// <summary>
    /// Adds soundboard services for playing sound effects in voice channels.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSoundboard(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<SoundboardOptions>(
            configuration.GetSection(SoundboardOptions.SectionName));
        services.Configure<SoundPlayLogRetentionOptions>(
            configuration.GetSection(SoundPlayLogRetentionOptions.SectionName));
        services.Configure<AudioCacheOptions>(
            configuration.GetSection(AudioCacheOptions.SectionName));

        // Soundboard services (scoped for per-request)
        services.AddScoped<ISoundService, SoundService>();
        services.AddScoped<ISoundFileService, SoundFileService>();
        services.AddScoped<IGuildAudioSettingsService, GuildAudioSettingsService>();

        // Audio cache service (singleton for connection pooling and shared state)
        services.AddSingleton<ISoundCacheService, SoundCacheService>();

        // Background services
        services.AddHostedService<SoundPlayLogRetentionService>();
        services.AddHostedService<AudioCacheCleanupService>();

        return services;
    }

    /// <summary>
    /// Adds text-to-speech services including Azure Speech integration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTts(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<AzureSpeechOptions>(
            configuration.GetSection(AzureSpeechOptions.SectionName));
        services.Configure<AzureSpeechSsmlOptions>(
            configuration.GetSection(AzureSpeechSsmlOptions.SectionName));

        // TTS data services (scoped for per-request)
        services.AddScoped<ITtsSettingsService, TtsSettingsService>();
        services.AddScoped<ITtsHistoryService, TtsHistoryService>();

        // SSML builder (transient for thread-safety)
        services.AddTransient<ISsmlBuilder, SsmlBuilder>();

        // SSML validator (transient for thread-safety)
        services.AddTransient<ISsmlValidator, SsmlValidator>();

        // Voice capability provider (singleton for caching)
        services.AddSingleton<IVoiceCapabilityProvider, VoiceCapabilityProvider>();

        // Style preset provider (singleton for static data)
        services.AddSingleton<IStylePresetProvider, StylePresetProvider>();

        // Azure Speech TTS service (singleton for connection pooling)
        services.AddSingleton<ITtsService, AzureTtsService>();

        return services;
    }

    /// <summary>
    /// Adds VOX clip library services for Half-Life style audio synthesis.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVox(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<VoxOptions>(
            configuration.GetSection(VoxOptions.SectionName));

        // VOX clip library (singleton for shared state and performance)
        services.AddSingleton<IVoxClipLibrary, VoxClipLibrary>();

        // VOX concatenation service (singleton for stateless processing)
        services.AddSingleton<IVoxConcatenationService, VoxConcatenationService>();

        return services;
    }
}
