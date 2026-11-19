using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Autocomplete;
using DiscordBot.Bot.Commands;
using DiscordBot.Core.Enums;
using FluentAssertions;
using DiscordBot.Tests.TestHelpers;

namespace DiscordBot.Tests.Commands;

/// <summary>
/// Verifies the public shape of the slash commands that were merged from near-duplicates
/// (<c>/vox</c>, <c>/tts</c>, <c>/join</c>, <c>/privacy</c>, <c>/rat</c>). Each module is built
/// by a real, unconnected <see cref="InteractionService"/>, so these tests read exactly what
/// Discord would be sent at registration.
/// </summary>
public class MergedCommandShapeTests : IAsyncLifetime
{
    private DiscordSocketClient _client = null!;
    private InteractionService _interactionService = null!;
    private AutoMockServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig { GatewayIntents = GatewayIntents.None });
        _interactionService = new InteractionService(_client);
        _services = new AutoMockServiceProvider(_client);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _interactionService.Dispose();
        _client.Dispose();
        return Task.CompletedTask;
    }

    private Task<ModuleInfo> BuildAsync<T>() => _interactionService.AddModuleAsync(typeof(T), _services);

    [Fact]
    public async Task VoxModule_ExposesSingleVoxCommand_WithVoiceChoices()
    {
        var module = await BuildAsync<VoxModule>();

        module.SlashGroupName.Should().BeNullOrEmpty();
        module.SlashCommands.Select(c => c.Name).Should().BeEquivalentTo("vox");

        var command = module.SlashCommands.Single();
        command.Parameters.Select(p => p.Name).Should().ContainInOrder("message", "voice", "gap");

        var voice = command.Parameters.Single(p => p.Name == "voice");
        voice.IsRequired.Should().BeFalse("vox is the default voice");
        voice.Choices.Select(c => c.Value).Should().BeEquivalentTo(new object[] { "vox", "fvox", "hgrunt" });

        command.Parameters.Single(p => p.Name == "message").IsAutocomplete.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, VoxClipGroup.Vox)]
    [InlineData("", VoxClipGroup.Vox)]
    [InlineData("vox", VoxClipGroup.Vox)]
    [InlineData("fvox", VoxClipGroup.Fvox)]
    [InlineData("HGRUNT", VoxClipGroup.Hgrunt)]
    [InlineData("unknown", VoxClipGroup.Vox)]
    public void VoxModule_ParseVoice_MapsOptionToClipGroup(string? voice, VoxClipGroup expected)
    {
        VoxModule.ParseVoice(voice).Should().Be(expected);
    }

    [Fact]
    public void VoxClipAutocomplete_ResolveGroup_FollowsTheVoiceOption()
    {
        var withFvox = new (string, object?)[] { ("message", "hello"), ("voice", "fvox") };
        var withoutVoice = new (string, object?)[] { ("message", "hello") };

        VoxClipAutocompleteHandler.ResolveGroup(withFvox).Should().Be(VoxClipGroup.Fvox);
        VoxClipAutocompleteHandler.ResolveGroup(withoutVoice).Should().Be(VoxClipGroup.Vox);
        VoxClipAutocompleteHandler.ResolveGroup(null).Should().Be(VoxClipGroup.Vox);
    }

    [Fact]
    public async Task TtsModule_ExposesSingleTtsCommand_WithOptionalVoiceAndPreset()
    {
        var module = await BuildAsync<TtsModule>();

        module.SlashCommands.Select(c => c.Name).Should().BeEquivalentTo("tts");

        var command = module.SlashCommands.Single();
        command.Parameters.Select(p => p.Name).Should().ContainInOrder("message", "voice", "preset");

        var voice = command.Parameters.Single(p => p.Name == "voice");
        var preset = command.Parameters.Single(p => p.Name == "preset");
        voice.IsRequired.Should().BeFalse();
        voice.IsAutocomplete.Should().BeTrue();
        preset.IsRequired.Should().BeFalse();
        preset.IsAutocomplete.Should().BeTrue("preset takes over the former /tts-styled autocomplete");
    }

    [Fact]
    public async Task VoiceModule_ExposesJoinWithOptionalChannel_AndLeave()
    {
        var module = await BuildAsync<VoiceModule>();

        module.SlashCommands.Select(c => c.Name).Should().BeEquivalentTo("join", "leave");

        var join = module.SlashCommands.Single(c => c.Name == "join");
        var channel = join.Parameters.Should().ContainSingle().Subject;
        channel.Name.Should().Be("channel");
        channel.IsRequired.Should().BeFalse("no channel joins the caller's voice channel");

        // /join must not require the caller to be in voice: with a channel given, it doesn't matter.
        join.Preconditions.Select(p => p.GetType().Name).Should().NotContain("RequireVoiceChannelAttribute");
    }

    [Fact]
    public async Task PrivacyModule_ContainsConsentAndDataSubcommands()
    {
        var module = await BuildAsync<PrivacyModule>();

        module.SlashGroupName.Should().Be("privacy");
        module.SlashCommands.Select(c => c.Name).Should().BeEquivalentTo(
            "consent-grant", "consent-revoke", "status", "preview-delete", "export-data", "delete-data");
    }

    [Fact]
    public async Task RatWatchModule_IsRatGroup_WithoutContextMenu()
    {
        var module = await BuildAsync<RatWatchModule>();

        module.SlashGroupName.Should().Be("rat");
        module.SlashCommands.Select(c => c.Name).Should().BeEquivalentTo("clear", "stats", "leaderboard");
        module.ContextCommands.Should().BeEmpty();
        module.ModalCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task RatWatchContextMenuModule_HoldsContextMenuAndModal_OutsideAnyGroup()
    {
        var module = await BuildAsync<RatWatchContextMenuModule>();

        module.SlashGroupName.Should().BeNullOrEmpty();
        module.SlashCommands.Should().BeEmpty();
        module.ContextCommands.Select(c => c.Name).Should().BeEquivalentTo("Rat Watch");
        module.ModalCommands.Select(c => c.Name).Should().BeEquivalentTo("ratwatch:create:*:*");
    }
}
