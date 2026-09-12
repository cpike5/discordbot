using FluentAssertions;

namespace DiscordBot.Evals;

/// <summary>
/// Does the skill mechanism pay for itself?
/// </summary>
/// <remarks>
/// <para>
/// Phase 5 shipped skills with one question left open, and it is empirical rather than structural:
/// whether the model loads the right skill often enough to be worth the round it costs. These cases
/// are the cheapest way to ask it. A skill loaded when it was not needed is as much a failure as one
/// not loaded when it was — the first costs a round trip on every unrelated question.
/// </para>
/// <para>
/// The evidence is <see cref="EvalRun.ActivatedSkills"/>, read off the run's own session, rather than
/// anything the reply says. The tools the DM skills name need a Discord client the harness does not
/// have, so they are narrowed away before the run starts; the skill still loads and its instructions
/// still arrive, which is exactly the part being measured.
/// </para>
/// </remarks>
public class SkillEvals
{
    [EvalFact]
    public async Task AskedAboutModeration_LoadsTheModerationSkill()
    {
        using var harness = new EvalHarness();

        var run = await harness.AskAsync("Who has been banned on my server recently?");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.ActivatedSkills.Should().Contain("moderation",
            "the moderation skill's summary is written as the condition that should trigger it");
    }

    [EvalFact]
    public async Task AskedAboutActivity_LoadsTheAnalyticsSkill()
    {
        using var harness = new EvalHarness();

        var run = await harness.AskAsync("How busy has the server been this month?");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.ActivatedSkills.Should().Contain("analytics");
    }

    [EvalFact]
    public async Task AskedSomethingNoSkillCovers_LoadsNone()
    {
        // The expensive failure mode. A model that loads a skill speculatively turns every question
        // into two round trips and warms nothing.
        using var harness = new EvalHarness();

        var run = await harness.AskAsync("Remember that I prefer short answers.");

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.ActivatedSkills.Should().BeEmpty("nothing in that request is about moderation or analytics");
        run.Called("save_note").Should().BeTrue("the right answer was a tool it already had");
    }

    [EvalFact]
    public async Task WithASkillAlreadyLoaded_DoesNotSpendARoundLoadingItAgain()
    {
        // What makes a skill nearly free from turn 2 on the DM surface: the activation is replayed,
        // its instructions are already in the prompt, and load_skill should not be called at all.
        using var harness = new EvalHarness();

        var run = await harness.AskAsync(
            "Any bans this week?", preActivatedSkills: new[] { "moderation" });

        run.Result.Success.Should().BeTrue(run.Result.ErrorMessage);
        run.Called("load_skill").Should().BeFalse(
            "a replayed activation is already in the prompt, so loading it again is a wasted round");
    }
}
