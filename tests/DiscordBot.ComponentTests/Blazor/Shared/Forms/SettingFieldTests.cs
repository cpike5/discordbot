using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class SettingFieldTests : BlazorComponentTestContext
{
    [Fact]
    public void Boolean_RendersToggle()
    {
        var setting = new SettingDto { Key = "a:b", Value = "true", DataType = SettingDataType.Boolean, DisplayName = "Feature" };
        var cut = Render<SettingField>(p => p.Add(x => x.Setting, setting));

        cut.FindComponent<Toggle>().Instance.Value.Should().BeTrue();
        cut.Markup.Should().Contain("Feature");
    }

    [Fact]
    public void AllowedValues_RendersSelect_RegardlessOfDataType()
    {
        var setting = new SettingDto
        {
            Key = "a:b",
            Value = "Info",
            DataType = SettingDataType.String,
            DisplayName = "Log Level",
            AllowedValues = new List<string> { "Debug", "Info", "Warning" }
        };
        var cut = Render<SettingField>(p => p.Add(x => x.Setting, setting));

        cut.FindComponent<Select<string>>().Instance.Options.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(SettingDataType.Integer)]
    [InlineData(SettingDataType.Decimal)]
    public void NumericType_RendersNumberTextInput(SettingDataType dataType)
    {
        var setting = new SettingDto { Key = "a:b", Value = "30", DataType = dataType, DisplayName = "Timeout" };
        var cut = Render<SettingField>(p => p.Add(x => x.Setting, setting));

        cut.Find("input[type='number']").Should().NotBeNull();
    }

    [Fact]
    public void StringType_RendersTextInput()
    {
        var setting = new SettingDto { Key = "a:b", Value = "hello", DataType = SettingDataType.String, DisplayName = "Name" };
        var cut = Render<SettingField>(p => p.Add(x => x.Setting, setting));

        cut.Find("input[type='text']").Should().NotBeNull();
    }

    [Fact]
    public void RequiresRestart_RendersBadge()
    {
        var setting = new SettingDto { Key = "a:b", Value = "true", DataType = SettingDataType.Boolean, DisplayName = "Feature", RequiresRestart = true };
        var cut = Render<SettingField>(p => p.Add(x => x.Setting, setting));

        cut.Markup.Should().Contain("Restart Required");
    }

    [Fact]
    public void NoRestartRequired_DoesNotRenderBadge()
    {
        var setting = new SettingDto { Key = "a:b", Value = "true", DataType = SettingDataType.Boolean, DisplayName = "Feature", RequiresRestart = false };
        var cut = Render<SettingField>(p => p.Add(x => x.Setting, setting));

        cut.Markup.Should().NotContain("Restart Required");
    }

    [Fact]
    public void OnValueChanged_FiresWithKeyAndNewValue_OnToggle()
    {
        (string Key, string Value)? captured = null;
        var setting = new SettingDto { Key = "my:setting", Value = "false", DataType = SettingDataType.Boolean, DisplayName = "Feature" };
        var cut = Render<SettingField>(p => p
            .Add(x => x.Setting, setting)
            .Add(x => x.OnValueChanged, EventCallback.Factory.Create<(string, string)>(this, v => captured = v)));

        cut.FindComponent<Toggle>().Find("input[type='checkbox']").Change(true);

        captured.Should().Be(("my:setting", "true"));
    }

    [Fact]
    public void OnValueChanged_FiresWithKeyAndNewValue_OnTextInput()
    {
        (string Key, string Value)? captured = null;
        var setting = new SettingDto { Key = "my:setting", Value = "old", DataType = SettingDataType.String, DisplayName = "Name" };
        var cut = Render<SettingField>(p => p
            .Add(x => x.Setting, setting)
            .Add(x => x.OnValueChanged, EventCallback.Factory.Create<(string, string)>(this, v => captured = v)));

        cut.Find("input[type='text']").Input("new");

        captured.Should().Be(("my:setting", "new"));
    }
}
