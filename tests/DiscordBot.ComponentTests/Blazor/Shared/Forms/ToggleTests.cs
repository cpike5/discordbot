using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class ToggleTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersLabelAndDescription()
    {
        var cut = Render<Toggle>(p => p.Add(x => x.Id, "t").Add(x => x.Label, "Enabled").Add(x => x.Description, "Turns it on"));

        cut.Markup.Should().Contain("Enabled");
        cut.Markup.Should().Contain("Turns it on");
    }

    [Fact]
    public void Value_True_ChecksInput()
    {
        var cut = Render<Toggle>(p => p.Add(x => x.Id, "t").Add(x => x.Value, true));
        cut.Find("input[type='checkbox']").HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public void ValueChanged_FiresOnChange()
    {
        bool? captured = null;
        var cut = Render<Toggle>(p => p
            .Add(x => x.Id, "t")
            .Add(x => x.Value, false)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<bool>(this, v => captured = v)));

        cut.Find("input[type='checkbox']").Change(true);

        captured.Should().BeTrue();
    }

    [Fact]
    public void SettingToggle_False_DoesNotEmitDataAttribute()
    {
        var cut = Render<Toggle>(p => p.Add(x => x.Id, "t"));
        cut.Find("input[type='checkbox']").HasAttribute("data-setting-toggle").Should().BeFalse();
    }

    [Fact]
    public void SettingToggle_True_EmitsDataAttribute_ForLegacyJs()
    {
        var cut = Render<Toggle>(p => p.Add(x => x.Id, "t").Add(x => x.SettingToggle, true));
        cut.Find("input[type='checkbox']").GetAttribute("data-setting-toggle").Should().Be("true");
    }

    [Fact]
    public void IsDisabled_SetsDisabledAttribute()
    {
        var cut = Render<Toggle>(p => p.Add(x => x.Id, "t").Add(x => x.IsDisabled, true));
        cut.Find("input[type='checkbox']").HasAttribute("disabled").Should().BeTrue();
    }
}
