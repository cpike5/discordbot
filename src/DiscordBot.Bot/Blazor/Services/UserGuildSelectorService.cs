using System.Security.Claims;
using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Blazor.Services;

/// <inheritdoc cref="IUserGuildSelectorService" />
internal sealed class UserGuildSelectorService : IUserGuildSelectorService
{
    private readonly IUserDiscordGuildService _userDiscordGuildService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DiscordSocketClient _discordClient;

    public UserGuildSelectorService(
        IUserDiscordGuildService userDiscordGuildService,
        UserManager<ApplicationUser> userManager,
        DiscordSocketClient discordClient)
    {
        _userDiscordGuildService = userDiscordGuildService;
        _userManager = userManager;
        _discordClient = discordClient;
    }

    public async Task<IReadOnlyList<GuildSelectorItem>> GetUserGuildsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var appUser = await _userManager.GetUserAsync(user);
        if (appUser == null)
        {
            return [];
        }

        var userGuilds = await _userDiscordGuildService.GetUserGuildsAsync(appUser.Id, cancellationToken);
        var botGuildIds = _discordClient.Guilds.Select(g => g.Id).ToHashSet();

        return userGuilds
            .Where(g => botGuildIds.Contains(g.GuildId))
            .Select(g => new GuildSelectorItem
            {
                GuildId = g.GuildId.ToString(),
                GuildName = g.GuildName,
                GuildIconUrl = g.GuildIconUrl
            })
            .OrderBy(g => g.GuildName)
            .ToList()
            .AsReadOnly();
    }
}
