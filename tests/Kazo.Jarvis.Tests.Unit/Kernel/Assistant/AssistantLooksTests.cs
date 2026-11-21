using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Permissions;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Assistant;

/// <summary>The look function for every combination of phase and conditions (ADR 0005, section 2; ADR 0011, section 4).</summary>
[TestFixture]
public sealed class AssistantLooksTests
{
    [Test]
    public void Every_combination_of_phase_and_conditions_gives_the_expected_look(
        [Values] AssistantPhase phase,
        [Values] bool fault,
        [Values] bool muted,
        [Values(0, 1, 4)] int delegatedTasks)
    {
        var state = AssistantState.Initial with
        {
            Phase = phase,
            Fault = fault ? new AssistantFault(FaultCause.ModelUnreachable) : null,
            MicrophoneMuted = muted,
            DelegatedTasks = delegatedTasks,
        };

        var expected = phase switch
        {
            AssistantPhase.Listening => AssistantLook.Listening,
            AssistantPhase.Thinking => AssistantLook.Thinking,
            AssistantPhase.Speaking => AssistantLook.Speaking,
            _ when fault => AssistantLook.Error,
            _ when muted => AssistantLook.MicrophoneMuted,
            _ when delegatedTasks > 0 => AssistantLook.LongTask,
            _ => AssistantLook.Idle,
        };

        Assert.That(AssistantLooks.Of(state), Is.EqualTo(expected));
        Assert.That(state.Look, Is.EqualTo(expected));
        Assert.That(AssistantLooks.ShowsProgressRing(state), Is.EqualTo(delegatedTasks > 0), "the ring is drawn over any look");
    }

    // Written out from the ADR, independent of the formula above.
    [TestCase(AssistantPhase.Idle, false, false, 0, AssistantLook.Idle)]
    [TestCase(AssistantPhase.Idle, false, false, 2, AssistantLook.LongTask)]
    [TestCase(AssistantPhase.Idle, false, true, 2, AssistantLook.MicrophoneMuted)]
    [TestCase(AssistantPhase.Idle, true, true, 2, AssistantLook.Error)]
    [TestCase(AssistantPhase.Listening, true, true, 2, AssistantLook.Listening)]
    [TestCase(AssistantPhase.Thinking, true, false, 0, AssistantLook.Thinking)]
    [TestCase(AssistantPhase.Speaking, false, true, 1, AssistantLook.Speaking)]
    public void Priority_of_the_looks(AssistantPhase phase, bool fault, bool muted, int tasks, AssistantLook expected)
    {
        var state = AssistantState.Initial with
        {
            Phase = phase,
            Fault = fault ? new AssistantFault(FaultCause.MicrophoneMissing) : null,
            MicrophoneMuted = muted,
            DelegatedTasks = tasks,
        };

        Assert.That(AssistantLooks.Of(state), Is.EqualTo(expected));
    }

    [Test]
    public void The_eight_looks_are_all_reachable()
    {
        var reached = new HashSet<AssistantLook>();
        foreach (var phase in Enum.GetValues<AssistantPhase>())
        {
            foreach (var fault in new[] { false, true })
            {
                foreach (var muted in new[] { false, true })
                {
                    foreach (var tasks in new[] { 0, 1 })
                    {
                        foreach (var pending in new[] { false, true })
                        {
                            reached.Add(AssistantLooks.Of(AssistantState.Initial with
                            {
                                Phase = phase,
                                Fault = fault ? new AssistantFault(FaultCause.VoiceMissing) : null,
                                MicrophoneMuted = muted,
                                DelegatedTasks = tasks,
                                Confirmation = pending ? new PendingConfirmation(Guid.NewGuid(), ActionClass.Sensitive) : null,
                            }));
                        }
                    }
                }
            }
        }

        Assert.That(reached, Is.EquivalentTo(Enum.GetValues<AssistantLook>()));
    }

    // ADR 0023, section 1: the confirmation look comes before the look of the phase and before every condition.
    [Test]
    public void A_pending_confirmation_wins_over_the_phase_and_the_other_conditions(
        [Values] AssistantPhase phase,
        [Values] bool fault,
        [Values] bool muted)
    {
        var state = AssistantState.Initial with
        {
            Phase = phase,
            Fault = fault ? new AssistantFault(FaultCause.VoiceMissing) : null,
            MicrophoneMuted = muted,
            DelegatedTasks = 1,
            Confirmation = new PendingConfirmation(Guid.NewGuid(), ActionClass.Critical),
        };

        Assert.That(AssistantLooks.Of(state), Is.EqualTo(AssistantLook.Confirmation));
    }
}
