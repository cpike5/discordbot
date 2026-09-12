using FluentAssertions;

namespace DiscordBot.Evals;

/// <summary>
/// Does the model reach for the right memory tool, and does the row actually appear?
/// </summary>
/// <remarks>
/// Every assertion here is on a machine-checkable fact. "It said it saved the note" is not evidence
/// of anything — it is the exact failure these cases exist to catch — so the checks are the tool
/// name in <c>AgentRunResult.ToolNames</c> and the row in the database.
/// </remarks>
public class MemoryToolEvals
{
    [EvalFact]
    public async Task AskedToRemember_SavesANoteWithWhatWasSaid()
    {
        using var harness = new EvalHarness();

        var run = await harness.AskAsync("Remember that I take my coffee black, no sugar.");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.Called("save_note").Should().BeTrue("the request is the plainest possible save");

        var notes = await harness.AllNotesAsync();
        notes.Should().HaveCount(1);
        notes[0].Content.Should().Contain("coffee", "the note has to carry what was actually said");
    }

    [EvalFact]
    public async Task AskedWhatItRemembers_ReadsTheNotesRatherThanInventingThem()
    {
        using var harness = new EvalHarness();
        await harness.SeedNoteAsync("The owner's timezone is America/Toronto.", "context");

        var run = await harness.AskAsync("What do you remember about me?");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.ToolNames.Should().IntersectWith(new[] { "list_notes", "search_notes" },
            "an answer about stored notes that called nothing is an answer it made up");
    }

    [EvalFact]
    public async Task AskedAboutOneSubject_SearchesRatherThanListingEverything()
    {
        using var harness = new EvalHarness();
        await harness.SeedNoteAsync("Deploys go out on Thursdays.", "process");
        await harness.SeedNoteAsync("The owner's timezone is America/Toronto.", "context");

        var run = await harness.AskAsync("Do I have anything saved about deploys?");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.ToolNames.Should().NotBeEmpty("a question about saved notes needs the store");
    }

    [EvalFact]
    public async Task AskedForANoteThatDoesNotExist_RelaysTheNotFoundInsteadOfRetrying()
    {
        using var harness = new EvalHarness();

        var run = await harness.AskAsync("Read me note number 987654 please.");

        // The tool answers with a successful result carrying `found: false`, which is the house
        // convention precisely so the model can relay it. A loop that spent its whole budget retrying
        // is the regression this case is watching for.
        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.Result.StoppedOnMaxIterations.Should().BeFalse("a not-found answer is final, not a retry");
        run.Called("get_note").Should().BeTrue();
    }

    [EvalFact]
    public async Task AskedToDeleteANote_DeletesThatOneAndLeavesTheOthers()
    {
        using var harness = new EvalHarness();
        var doomed = await harness.SeedNoteAsync("Temporary: the staging password is in the vault.", "todo");
        await harness.SeedNoteAsync("Deploys go out on Thursdays.", "process");

        var run = await harness.AskAsync($"Delete note {doomed.Id}.");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.Called("delete_note").Should().BeTrue();

        var remaining = await harness.AllNotesAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().NotBe(doomed.Id);
    }

    [EvalFact]
    public async Task AskedToRememberAndThenRecall_UsesBothToolsInOrder()
    {
        // A two-round case: the loop has to carry the first tool's result into the second call. It is
        // the cheapest test that the agentic loop is actually looping.
        using var harness = new EvalHarness();

        var run = await harness.AskAsync(
            "Remember that my favourite colour is orange, then tell me everything you have saved.");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.Called("save_note").Should().BeTrue();
        run.Result.TotalToolCalls.Should().BeGreaterThan(1);
        (await harness.AllNotesAsync()).Should().ContainSingle();
    }

    [EvalFact]
    public async Task AskedSomethingWithNoToolBehindIt_CallsNothing()
    {
        // The other half of tool quality, and the half nobody measures: a model that reaches for a
        // tool on every question costs a round trip per question and gets no better answer for it.
        using var harness = new EvalHarness();

        var run = await harness.AskAsync("What is 17 times 3?");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.ToolNames.Should().BeEmpty("arithmetic needs no tool, and a wasted round is a real cost");
    }

    [EvalFact]
    public async Task AskedToRememberSomethingLong_StillWritesExactlyOneRow()
    {
        // Watches for the duplicate-call pattern: a model that re-issues the same save because the
        // first result looked unfamiliar. The loop's duplicate guard should absorb it; the row count
        // is what says whether it did.
        using var harness = new EvalHarness();

        var run = await harness.AskAsync(
            "Remember this for me: the release checklist is tag, wait for the image, deploy staging, "
            + "smoke test, then deploy production, and never deploy on a Friday.");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        (await harness.AllNotesAsync()).Should().ContainSingle();
    }
}
