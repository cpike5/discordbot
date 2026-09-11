using System.Collections.Generic;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Configuration;

/// <summary>
/// Verifies that <see cref="AssistantOptions"/> binds correctly whether configuration uses the
/// historical flat keys (e.g. "Assistant:MaxTokens"), the new nested keys (e.g.
/// "Assistant:Sampling:MaxTokens"), or neither (defaults). Also verifies the documented
/// precedence when both a flat legacy key and its nested equivalent are present.
/// </summary>
public class AssistantOptionsBindingTests
{
#pragma warning disable CS0618 // Intentionally exercising the obsolete flat forwarding properties.

    private static AssistantOptions Bind(Dictionary<string, string?> data)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        var options = new AssistantOptions();
        configuration.GetSection(AssistantOptions.SectionName).Bind(options);
        return options;
    }

    [Fact]
    public void Bind_WithNoConfiguration_UsesDefaults()
    {
        var options = Bind(new Dictionary<string, string?>());

        options.GloballyEnabled.Should().BeFalse();
        options.Sampling.Model.Should().Be("openrouter/auto");
        options.Sampling.MaxTokens.Should().Be(512);
        options.Sampling.Temperature.Should().Be(0.3);
        options.RateLimits.DefaultRateLimit.Should().Be(5);
        options.RateLimits.RateLimitWindowMinutes.Should().Be(5);
        options.Messages.MaxQuestionLength.Should().Be(500);
        options.Messages.MaxResponseLength.Should().Be(1800);
        options.Tools.EnableDocumentationTools.Should().BeTrue();
        options.Cost.CostPerMillionInputTokens.Should().Be(3.00m);
        options.Privacy.RequireExplicitConsent.Should().BeTrue();

        // Legacy forwarding properties reflect the same defaults.
        options.MaxTokens.Should().Be(512);
        options.DefaultRateLimit.Should().Be(5);
    }

    [Fact]
    public void Bind_WithOldFlatKeys_PopulatesNestedOptions()
    {
        var data = new Dictionary<string, string?>
        {
            [$"{AssistantOptions.SectionName}:GloballyEnabled"] = "true",
            [$"{AssistantOptions.SectionName}:Model"] = "anthropic/claude-opus-4",
            [$"{AssistantOptions.SectionName}:MaxTokens"] = "1024",
            [$"{AssistantOptions.SectionName}:Temperature"] = "0.2",
            [$"{AssistantOptions.SectionName}:DefaultRateLimit"] = "10",
            [$"{AssistantOptions.SectionName}:RateLimitWindowMinutes"] = "15",
            [$"{AssistantOptions.SectionName}:MaxQuestionLength"] = "750",
            [$"{AssistantOptions.SectionName}:EnableDocumentationTools"] = "false",
            [$"{AssistantOptions.SectionName}:CostPerMillionInputTokens"] = "9.99",
            [$"{AssistantOptions.SectionName}:RequireExplicitConsent"] = "false",
        };

        var options = Bind(data);

        options.GloballyEnabled.Should().BeTrue();
        options.Sampling.Model.Should().Be("anthropic/claude-opus-4");
        options.Sampling.MaxTokens.Should().Be(1024);
        options.Sampling.Temperature.Should().Be(0.2);
        options.RateLimits.DefaultRateLimit.Should().Be(10);
        options.RateLimits.RateLimitWindowMinutes.Should().Be(15);
        options.Messages.MaxQuestionLength.Should().Be(750);
        options.Tools.EnableDocumentationTools.Should().BeFalse();
        options.Cost.CostPerMillionInputTokens.Should().Be(9.99m);
        options.Privacy.RequireExplicitConsent.Should().BeFalse();
    }

    [Fact]
    public void Bind_WithNewNestedKeys_PopulatesNestedOptions()
    {
        var data = new Dictionary<string, string?>
        {
            [$"{AssistantOptions.SectionName}:GloballyEnabled"] = "true",
            [$"{AssistantOptions.SectionName}:Sampling:Model"] = "anthropic/claude-opus-4",
            [$"{AssistantOptions.SectionName}:Sampling:MaxTokens"] = "1024",
            [$"{AssistantOptions.SectionName}:Sampling:Temperature"] = "0.2",
            [$"{AssistantOptions.SectionName}:RateLimits:DefaultRateLimit"] = "10",
            [$"{AssistantOptions.SectionName}:RateLimits:RateLimitWindowMinutes"] = "15",
            [$"{AssistantOptions.SectionName}:Messages:MaxQuestionLength"] = "750",
            [$"{AssistantOptions.SectionName}:Tools:EnableDocumentationTools"] = "false",
            [$"{AssistantOptions.SectionName}:Cost:CostPerMillionInputTokens"] = "9.99",
            [$"{AssistantOptions.SectionName}:Privacy:RequireExplicitConsent"] = "false",
        };

        var options = Bind(data);

        options.GloballyEnabled.Should().BeTrue();
        options.Sampling.Model.Should().Be("anthropic/claude-opus-4");
        options.Sampling.MaxTokens.Should().Be(1024);
        options.Sampling.Temperature.Should().Be(0.2);
        options.RateLimits.DefaultRateLimit.Should().Be(10);
        options.RateLimits.RateLimitWindowMinutes.Should().Be(15);
        options.Messages.MaxQuestionLength.Should().Be(750);
        options.Tools.EnableDocumentationTools.Should().BeFalse();
        options.Cost.CostPerMillionInputTokens.Should().Be(9.99m);
        options.Privacy.RequireExplicitConsent.Should().BeFalse();

        // Legacy forwarding properties read through to the same nested values.
        options.Model.Should().Be("anthropic/claude-opus-4");
        options.MaxTokens.Should().Be(1024);
        options.DefaultRateLimit.Should().Be(10);
    }

    [Fact]
    public void Bind_WithBothFlatAndNestedKeys_FlatKeyTakesPrecedence()
    {
        // Documented precedence: when both the legacy flat key and the new nested key are set,
        // the flat key wins (AssistantOptions declares nested option properties before the
        // obsolete flat forwarding properties, so ConfigurationBinder applies the flat value last).
        var data = new Dictionary<string, string?>
        {
            [$"{AssistantOptions.SectionName}:MaxTokens"] = "111",
            [$"{AssistantOptions.SectionName}:Sampling:MaxTokens"] = "222",
            [$"{AssistantOptions.SectionName}:DefaultRateLimit"] = "7",
            [$"{AssistantOptions.SectionName}:RateLimits:DefaultRateLimit"] = "42",
        };

        var options = Bind(data);

        options.Sampling.MaxTokens.Should().Be(111, "the flat legacy key must win when both are present");
        options.RateLimits.DefaultRateLimit.Should().Be(7, "the flat legacy key must win when both are present");
    }

    [Fact]
    public void RealDiRegistration_WithBothFlatAndNestedKeys_FlatKeyTakesPrecedenceViaPostConfigure()
    {
        // Exercises the actual production wiring — services.AddOptions() + AssistantServiceExtensions.AddAssistant
        // — rather than a raw ConfigurationBinder.Bind call, so the PostConfigure<AssistantOptions> precedence
        // step (see AssistantServiceExtensions.ApplyFlatLegacyKeyPrecedence) is genuinely under test, not just
        // the coincidental declaration-order behavior of plain binding.
        var data = new Dictionary<string, string?>
        {
            [$"{AssistantOptions.SectionName}:MaxTokens"] = "111",
            [$"{AssistantOptions.SectionName}:Sampling:MaxTokens"] = "222",
            [$"{AssistantOptions.SectionName}:DefaultRateLimit"] = "7",
            [$"{AssistantOptions.SectionName}:RateLimits:DefaultRateLimit"] = "42",
            // No OpenRouter:ApiKey — keeps the LLM-dependent registrations (which need a live DbContext etc.) out of the container.
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        var services = new ServiceCollection();
        services.AddAssistant(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AssistantOptions>>().Value;

        options.Sampling.MaxTokens.Should().Be(111, "the flat legacy key must win, enforced explicitly by PostConfigure");
        options.RateLimits.DefaultRateLimit.Should().Be(7, "the flat legacy key must win, enforced explicitly by PostConfigure");
    }

    [Fact]
    public void Bind_WithNoConfiguration_DefaultsMaxToolRoundsToEight()
    {
        var options = Bind(new Dictionary<string, string?>());

        options.Tools.MaxToolRounds.Should().Be(8);
        options.Tools.MaxToolCallsPerQuestion.Should().Be(8, "the old name forwards to the new one");
    }

    [Fact]
    public void Bind_WithNestedLegacyToolKey_StillWinsOverTheNewName()
    {
        // "MaxToolCallsPerQuestion" named calls but has always limited rounds. Both keys bind to
        // the same field, so which one wins cannot be left to property declaration order.
        var data = new Dictionary<string, string?>
        {
            [$"{AssistantOptions.SectionName}:Tools:MaxToolRounds"] = "12",
            [$"{AssistantOptions.SectionName}:Tools:MaxToolCallsPerQuestion"] = "4",
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        var services = new ServiceCollection();
        services.AddAssistant(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AssistantOptions>>().Value;

        options.Tools.MaxToolRounds.Should().Be(4, "the legacy key must keep winning when both are set");
    }

    [Fact]
    public void Bind_WithFlatLegacyToolKey_WinsOverBothNestedKeys()
    {
        var data = new Dictionary<string, string?>
        {
            [$"{AssistantOptions.SectionName}:MaxToolCallsPerQuestion"] = "3",
            [$"{AssistantOptions.SectionName}:Tools:MaxToolCallsPerQuestion"] = "4",
            [$"{AssistantOptions.SectionName}:Tools:MaxToolRounds"] = "12",
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        var services = new ServiceCollection();
        services.AddAssistant(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AssistantOptions>>().Value;

        options.Tools.MaxToolRounds.Should().Be(3, "the oldest spelling of all still wins");
    }

    [Fact]
    public void Bind_WithOnlyTheNewToolKey_UsesIt()
    {
        var data = new Dictionary<string, string?>
        {
            [$"{AssistantOptions.SectionName}:Tools:MaxToolRounds"] = "12",
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        var services = new ServiceCollection();
        services.AddAssistant(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AssistantOptions>>().Value;

        options.Tools.MaxToolRounds.Should().Be(12);
    }

#pragma warning restore CS0618
}
