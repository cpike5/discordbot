using System.Text.Json;
using DiscordBot.Agents;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="ToolInput"/> — reading a model's arguments, and describing them.
/// </summary>
public class ToolInputTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    public void GetString_ReadsAString()
    {
        ToolInput.GetString(Json("""{"content":"hello"}"""), "content").Should().Be("hello");
    }

    [Theory]
    [InlineData("{}")]                       // missing
    [InlineData("""{"content":null}""")]     // explicit null
    [InlineData("""{"content":""}""")]       // empty
    [InlineData("""{"content":"   "}""")]    // blank
    [InlineData("""{"content":7}""")]        // wrong kind
    [InlineData("""{"content":["a"]}""")]    // wrong kind
    [InlineData("[1,2]")]                    // input isn't an object
    public void GetString_IsNull_WhenTheModelDidNotReallyGiveOne(string raw)
    {
        // Missing, null, blank and wrong-kind are the same thing to a tool, so they get one answer.
        ToolInput.GetString(Json(raw), "content").Should().BeNull();
    }

    [Fact]
    public void GetInt_ReadsANumber()
    {
        ToolInput.GetInt(Json("""{"limit":25}"""), "limit").Should().Be(25);
    }

    [Fact]
    public void GetInt_ReadsAQuotedNumber()
    {
        // Models do this. Refusing it teaches them nothing and costs the run a round trip.
        ToolInput.GetInt(Json("""{"limit":"25"}"""), "limit").Should().Be(25);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"limit":"lots"}""")]
    [InlineData("""{"limit":true}""")]
    [InlineData("""{"limit":1.5}""")]
    public void GetInt_IsNull_WhenUnreadable(string raw)
    {
        ToolInput.GetInt(Json(raw), "limit").Should().BeNull();
    }

    [Fact]
    public void GetInt_IsNull_WhenTheNumberDoesNotFit()
    {
        ToolInput.GetInt(Json("""{"limit":99999999999}"""), "limit").Should().BeNull();
    }

    [Theory]
    [InlineData("{}", 20)]                        // fallback
    [InlineData("""{"limit":5}""", 5)]            // in range
    [InlineData("""{"limit":0}""", 1)]            // clamped up
    [InlineData("""{"limit":9000}""", 50)]        // clamped down
    [InlineData("""{"limit":"nonsense"}""", 20)]  // fallback
    public void GetInt_WithBounds_ClampsOrFallsBack(string raw, int expected)
    {
        ToolInput.GetInt(Json(raw), "limit", fallback: 20, min: 1, max: 50).Should().Be(expected);
    }

    [Fact]
    public void GetLong_ReadsALargeNumber()
    {
        ToolInput.GetLong(Json("""{"note_id":9007199254740993}"""), "note_id")
            .Should().Be(9007199254740993L);
    }

    [Fact]
    public void GetUInt64_ReadsASnowflakeInEitherForm()
    {
        // Snowflakes exceed JavaScript's safe integer range, so the quoted form is the common one.
        ToolInput.GetUInt64(Json("""{"guild_id":"1234567890123456789"}"""), "guild_id")
            .Should().Be(1234567890123456789UL);
        ToolInput.GetUInt64(Json("""{"guild_id":1234567890123456789}"""), "guild_id")
            .Should().Be(1234567890123456789UL);
    }

    [Theory]
    [InlineData("""{"flag":true}""", true)]
    [InlineData("""{"flag":false}""", false)]
    [InlineData("""{"flag":"true"}""", true)]
    [InlineData("""{"flag":"False"}""", false)]
    public void GetBool_ReadsABooleanInEitherForm(string raw, bool expected)
    {
        ToolInput.GetBool(Json(raw), "flag").Should().Be(expected);
    }

    [Fact]
    public void GetBool_IsNull_WhenUnreadable()
    {
        ToolInput.GetBool(Json("""{"flag":1}"""), "flag").Should().BeNull();
    }

    [Fact]
    public void GetStringArray_KeepsTheUsableEntries()
    {
        ToolInput.GetStringArray(Json("""{"tags":["a",null,"","  ","b",3]}"""), "tags")
            .Should().Equal("a", "b");
    }

    [Fact]
    public void GetStringArray_IsEmptyForAnEmptyArray_ButNullForNoArray()
    {
        // "The model sent an empty list" and "the model sent nothing" are different answers.
        ToolInput.GetStringArray(Json("""{"tags":[]}"""), "tags").Should().BeEmpty();
        ToolInput.GetStringArray(Json("{}"), "tags").Should().BeNull();
        ToolInput.GetStringArray(Json("""{"tags":"a"}"""), "tags").Should().BeNull();
    }

    [Fact]
    public void Schema_WritesTypeAndDescription()
    {
        var json = JsonSerializer.Serialize(
            ToolInput.Schema("string", "A note."), ToolJson.Compact);

        json.Should().Be("""{"type":"string","description":"A note."}""");
    }

    [Fact]
    public void Schema_WritesTheOptionalKeywordsInAFixedOrder()
    {
        // The tool array is the prompt cache's prefix, so a schema's byte order is worth pinning.
        var json = JsonSerializer.Serialize(
            ToolInput.Schema("integer", "How many.", @default: 10, minimum: 1, maximum: 50),
            ToolJson.Compact);

        json.Should().Be(
            """{"type":"integer","description":"How many.","default":10,"minimum":1,"maximum":50}""");
    }

    [Fact]
    public void Schema_OmitsTheKeywordsItWasNotGiven()
    {
        JsonSerializer.Serialize(ToolInput.Schema("string", "A tag."), ToolJson.Compact)
            .Should().NotContain("minimum").And.NotContain("default");
    }

    [Fact]
    public void Schema_DescribesAnArraysItems()
    {
        JsonSerializer.Serialize(
                ToolInput.Schema("array", "Tags to match.", itemType: "string"), ToolJson.Compact)
            .Should().Be("""{"type":"array","description":"Tags to match.","items":{"type":"string"}}""");
    }

    [Fact]
    public void Schema_RefusesAMissingDescription()
    {
        // A property with a bare type is a parameter the model will guess at.
        var act = () => ToolInput.Schema("string", "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ObjectSchema_WritesAnObjectSchemaWithItsRequiredList()
    {
        var schema = ToolInput.ObjectSchema(
            new
            {
                note_id = ToolInput.Schema("integer", "The ID of the note.")
            },
            "note_id");

        JsonSerializer.Serialize(schema, ToolJson.Compact).Should().Be(
            """{"type":"object","properties":{"note_id":{"type":"integer","description":"The ID of the note."}},"required":["note_id"]}""");
    }

    [Fact]
    public void ObjectSchema_StillWritesAnEmptyRequiredList()
    {
        // Emitted rather than omitted: a schema whose shape changes with its content is a schema
        // whose cached prefix changes for no reason.
        JsonSerializer.Serialize(
                ToolInput.ObjectSchema(new { tag = ToolInput.Schema("string", "A tag.") }),
                ToolJson.Compact)
            .Should().EndWith(""","required":[]}""");
    }
}
