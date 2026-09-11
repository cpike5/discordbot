using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class SelectTests : BlazorComponentTestContext
{
    private static readonly List<SelectOption> Options = new()
    {
        new SelectOption { Value = "a", Text = "Option A" },
        new SelectOption { Value = "b", Text = "Option B" }
    };

    [Fact]
    public void RendersOptions_WithSelectedValueMarked()
    {
        var cut = Render<Select<string>>(p => p
            .Add(x => x.Id, "s")
            .Add(x => x.Options, Options)
            .Add(x => x.Value, "b"));

        var options = cut.FindAll("option").Where(o => !string.IsNullOrEmpty(o.GetAttribute("value"))).ToList();
        options.Should().HaveCount(2);
        options.Single(o => o.GetAttribute("value") == "b").HasAttribute("selected").Should().BeTrue();
        options.Single(o => o.GetAttribute("value") == "a").HasAttribute("selected").Should().BeFalse();
    }

    [Fact]
    public void Placeholder_RendersDisabledOption_WhenNoValueSelected()
    {
        var cut = Render<Select<string>>(p => p.Add(x => x.Id, "s").Add(x => x.Options, Options).Add(x => x.Placeholder, "Choose..."));

        var placeholder = cut.Find("option[value='']");
        placeholder.TextContent.Should().Be("Choose...");
        placeholder.HasAttribute("disabled").Should().BeTrue();
        placeholder.HasAttribute("selected").Should().BeTrue();
    }

    [Fact]
    public void OptionGroups_RenderOptgroupsInsteadOfFlatOptions()
    {
        var groups = new List<SelectOptionGroup>
        {
            new() { Label = "Group 1", Options = new List<SelectOption> { new() { Value = "x", Text = "X" } } },
            new() { Label = "Group 2", Options = new List<SelectOption> { new() { Value = "y", Text = "Y" } } }
        };

        var cut = Render<Select<string>>(p => p.Add(x => x.Id, "s").Add(x => x.OptionGroups, groups));

        var optgroups = cut.FindAll("optgroup");
        optgroups.Should().HaveCount(2);
        optgroups[0].GetAttribute("label").Should().Be("Group 1");
    }

    [Fact]
    public void ValueChanged_FiresOnChange_WithConvertedValue()
    {
        string? captured = null;
        var cut = Render<Select<string>>(p => p
            .Add(x => x.Id, "s")
            .Add(x => x.Options, Options)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        cut.Find("select").Change("b");

        captured.Should().Be("b");
    }

    [Fact]
    public void GenericTValue_Enum_ConvertsThroughBindConverter()
    {
        var sizeOptions = new List<SelectOption>
        {
            new() { Value = "Small", Text = "Small" },
            new() { Value = "Large", Text = "Large" }
        };

        InputSize? captured = null;

        var cut = Render<Select<InputSize>>((ComponentParameterCollectionBuilder<Select<InputSize>> p) =>
        {
            p.Add(x => x.Id, "s");
            p.Add(x => x.Options, sizeOptions);
            p.Add(x => x.ValueChanged, EventCallback.Factory.Create<InputSize>(this, v => captured = v));
        });

        cut.Find("select").Change("Large");

        captured.Should().Be(InputSize.Large);
    }

    [Fact]
    public void AllowMultiple_UsesSelectedValues_NotValue()
    {
        IReadOnlyList<string>? captured = null;
        var cut = Render<Select<string>>(p => p
            .Add(x => x.Id, "s")
            .Add(x => x.Options, Options)
            .Add(x => x.AllowMultiple, true)
            .Add(x => x.SelectedValuesChanged, EventCallback.Factory.Create<IReadOnlyList<string>>(this, v => captured = v)));

        cut.Find("select").HasAttribute("multiple").Should().BeTrue();

        cut.Find("select").Change(new[] { "a", "b" });

        captured.Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void ValidationState_Error_AppliesBorderClass()
    {
        var cut = Render<Select<string>>(p => p
            .Add(x => x.Id, "s")
            .Add(x => x.Options, Options)
            .Add(x => x.ValidationState, ValidationState.Error)
            .Add(x => x.ValidationMessage, "Required"));

        cut.Find("select").ClassList.Should().Contain("border-error");
        cut.Markup.Should().Contain("Required");
    }

    [Fact]
    public void IsDisabled_SetsDisabledAttribute()
    {
        var cut = Render<Select<string>>(p => p.Add(x => x.Id, "s").Add(x => x.Options, Options).Add(x => x.IsDisabled, true));
        cut.Find("select").HasAttribute("disabled").Should().BeTrue();
    }
}
