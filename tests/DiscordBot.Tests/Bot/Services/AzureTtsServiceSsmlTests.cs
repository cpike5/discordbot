using System.Xml.Linq;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// The SSML that <see cref="AzureTtsService"/> sends to Azure for plain-text TTS.
/// </summary>
public class AzureTtsServiceSsmlTests
{
    [Fact]
    public void BuildSsml_WithAQuoteInTheVoiceName_StaysOneWellFormedVoiceElement()
    {
        var options = new TtsOptions { Voice = "en-US-JennyNeural\"><audio src=\"https://evil.example/x.wav\"/><voice name=\"x" };

        var ssml = AzureTtsService.BuildSsml("hello", options);

        var document = XDocument.Parse(ssml);
        XNamespace ns = "http://www.w3.org/2001/10/synthesis";
        var voices = document.Descendants(ns + "voice").ToList();
        voices.Should().ContainSingle();
        voices[0].Attribute("name")!.Value.Should().Be(options.Voice, "the voice name is data, not markup");
        document.Descendants(ns + "audio").Should().BeEmpty();
    }

    [Fact]
    public void BuildSsml_WithAnOrdinaryVoice_UsesItAsIs()
    {
        var ssml = AzureTtsService.BuildSsml("hello", new TtsOptions { Voice = "en-US-JennyNeural" });

        ssml.Should().Contain("<voice name=\"en-US-JennyNeural\">");
    }
}
