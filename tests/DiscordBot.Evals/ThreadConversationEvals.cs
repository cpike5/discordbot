using FluentAssertions;

namespace DiscordBot.Evals;

/// <summary>
/// Does a thread actually carry its conversation from one turn to the next?
/// </summary>
/// <remarks>
/// The evidence is rows and seeded history, never the reply's wording: a model that says "as I
/// mentioned" with nothing in its history is exactly the failure these cases would miss if they
/// read the text.
/// </remarks>
public class ThreadConversationEvals
{
    [EvalFact]
    public async Task SecondTurn_IsSeededWithTheFirst_AndBothAreSaved()
    {
        using var harness = new ThreadEvalHarness();

        var first = await harness.TurnAsync("How do I use text-to-speech in a voice channel?");
        first.Result.Success.Should().BeTrue(first.Result.ErrorMessage);
        first.HistoryMessagesSeeded.Should().Be(0, "a new thread has nothing to seed");

        var second = await harness.TurnAsync("And how do I pick a different voice for it?");
        second.Result.Success.Should().BeTrue(second.Result.ErrorMessage);
        second.HistoryMessagesSeeded.Should().Be(2, "the first question and its answer");

        var turns = await harness.SavedTurnsAsync();
        turns.Select(t => t.Role).Should().Equal("user", "assistant", "user", "assistant");
        turns[0].Content.Should().Contain("text-to-speech");

        var thread = await harness.ThreadAsync();
        thread.TurnCount.Should().Be(2);

        var logs = await harness.LogsAsync();
        logs.Should().HaveCount(2, "every turn is logged against its thread");
    }
}
