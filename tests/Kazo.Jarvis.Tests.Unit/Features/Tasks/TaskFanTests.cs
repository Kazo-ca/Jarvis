using System.Text.Json;
using Kazo.Jarvis.Features.Tasks.Board;
using Kazo.Jarvis.Features.Tasks.Fan;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Tasks;

/// <summary>The task fan (work item stage2/14-task-fan; ADR 0010; ADR 0013, section 6).</summary>
[TestFixture]
public sealed class TaskFanTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task A_double_click_opens_the_fan_from_where_the_orb_is_and_a_second_one_puts_it_away_after_the_closing()
    {
        var time = new FakeTimeProvider(Start);
        var surface = new Surface { Anchor = new FanAnchor(0.1, 0.2) };
        var fan = new TaskFan(time, surface);
        var changes = 0;
        fan.Changed += () => changes++;

        await fan.HandleAsync(new TaskFanToggleRequested(), CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(fan.Phase, Is.EqualTo(FanPhase.Open));
            Assert.That(fan.Anchor, Is.EqualTo(new FanAnchor(0.1, 0.2)));
            Assert.That(surface.Shown, Is.True);
            Assert.That(changes, Is.EqualTo(1));
        });

        await fan.HandleAsync(new TaskFanToggleRequested(), CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(fan.Phase, Is.EqualTo(FanPhase.Closing));
            Assert.That(surface.Shown, Is.True, "the surface stays while the panels go back into the orb");
        });

        time.Advance(TaskFan.ClosingTime);
        await WaitAsync(() => fan.Phase == FanPhase.Closed);
        Assert.Multiple(() =>
        {
            Assert.That(surface.Shown, Is.False);
            Assert.That(changes, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task A_fan_opened_again_while_it_closes_keeps_its_surface()
    {
        var time = new FakeTimeProvider(Start);
        var surface = new Surface();
        var fan = new TaskFan(time, surface);

        fan.Open();
        fan.Close();
        fan.Toggle();
        Assert.That(fan.Phase, Is.EqualTo(FanPhase.Open));

        time.Advance(TaskFan.ClosingTime + TaskFan.ClosingTime);
        await Task.Delay(50);
        Assert.Multiple(() =>
        {
            Assert.That(fan.Phase, Is.EqualTo(FanPhase.Open));
            Assert.That(surface.Shown, Is.True);
            Assert.That(surface.Hides, Is.Zero);
        });
    }

    [Test]
    public void Without_a_surface_there_is_no_fan()
    {
        var fan = new TaskFan(new FakeTimeProvider(Start));
        fan.Toggle();
        Assert.That(fan.Phase, Is.EqualTo(FanPhase.Closed));
    }

    [Test]
    public void The_fan_opens_toward_the_centre_of_the_screen_from_the_nearest_corner()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FanLayout.Direction(new FanAnchor(0.94, 0.9)), Is.EqualTo((-1, -1)));
            Assert.That(FanLayout.Direction(new FanAnchor(0.06, 0.9)), Is.EqualTo((1, -1)));
            Assert.That(FanLayout.Direction(new FanAnchor(0.06, 0.1)), Is.EqualTo((1, 1)));
            Assert.That(FanLayout.Direction(new FanAnchor(0.94, 0.1)), Is.EqualTo((-1, 1)));

            // Outside a corner: the arrangement of the nearest one.
            Assert.That(FanLayout.Direction(new FanAnchor(0.4, 0.7)), Is.EqualTo((1, -1)));
        });
    }

    [Test]
    public void The_panels_never_overlap_and_all_stand_on_the_side_of_the_centre([Range(1, FanLayout.MostPanels)] int count, [Values(0.06, 0.94)] double x, [Values(0.1, 0.9)] double y)
    {
        var anchor = new FanAnchor(x, y);
        var (horizontal, vertical) = FanLayout.Direction(anchor);
        var slots = FanLayout.Slots(anchor, count);

        Assert.That(slots, Has.Count.EqualTo(count));
        foreach (var slot in slots)
        {
            Assert.That(Math.Sign(slot.X), Is.EqualTo(horizontal));
            Assert.That(Math.Sign(slot.Y), Is.EqualTo(vertical));
        }

        for (var first = 0; first < slots.Count; first++)
        {
            for (var second = first + 1; second < slots.Count; second++)
            {
                var apart = Math.Abs(slots[first].X - slots[second].X) >= FanLayout.PanelWidth
                    || Math.Abs(slots[first].Y - slots[second].Y) >= FanLayout.PanelHeight;
                Assert.That(apart, Is.True, $"panels {first} and {second} of {count} overlap");
            }
        }
    }

    [Test]
    public void The_arc_holds_five_panels_at_most()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FanLayout.Slots(FanAnchor.BottomRight, 9), Has.Count.EqualTo(FanLayout.MostPanels));
            Assert.That(FanLayout.Slots(FanAnchor.BottomRight, 0), Is.Empty);
        });
    }

    [Test]
    public void One_panel_per_task_that_is_not_over_the_ones_that_wait_first_and_the_sub_tasks_inside()
    {
        var tasks = new[]
        {
            Work("a", WorkTaskStatus.Working, minutes: 5),
            Work("b", WorkTaskStatus.WaitingApproval, minutes: 1),
            Work("c", WorkTaskStatus.Done, minutes: 9),
            Work("d", WorkTaskStatus.Abandoned, minutes: 8),
            Work("e", WorkTaskStatus.Idle, minutes: 7),
            Work("a1", WorkTaskStatus.Working, minutes: 6, parent: "a"),
            Work("a2", WorkTaskStatus.Working, minutes: 7, parent: "a"),
            Work("a3", WorkTaskStatus.Working, minutes: 8, parent: "a"),
            Work("a4", WorkTaskStatus.Done, minutes: 9, parent: "a"),
        };

        var content = FanContent.Of(tasks, null);

        Assert.Multiple(() =>
        {
            Assert.That(content.Panels.Select(panel => panel.Task!.Id), Is.EqualTo(new[] { "b", "a", "e" }));
            Assert.That(content.Panels.Select(panel => panel.Amber), Is.EqualTo(new[] { true, false, false }));
            Assert.That(content.Tasks, Is.EqualTo(3));
            Assert.That(content.Decisions, Is.EqualTo(1));
            Assert.That(content.Hidden, Is.Zero);
            Assert.That(content.Panels[1].SubTasks.Select(child => child.Id), Is.EqualTo(new[] { "a1", "a2" }));
            Assert.That(content.Panels[1].MoreSubTasks, Is.EqualTo(1));
        });
    }

    [Test]
    public void The_request_of_a_session_is_shown_in_the_panel_of_its_task_with_its_buttons()
    {
        var tasks = new[] { Work("a", WorkTaskStatus.Working, minutes: 5), Work("b", WorkTaskStatus.WaitingApproval, minutes: 1) };
        var open = Open("b");

        var content = FanContent.Of(tasks, open);

        Assert.Multiple(() =>
        {
            Assert.That(content.Panels, Has.Count.EqualTo(2));
            Assert.That(content.Panels[0].Task!.Id, Is.EqualTo("b"));
            Assert.That(content.Panels[0].Decision, Is.SameAs(open));
            Assert.That(content.Panels[1].Decision, Is.Null);
            Assert.That(content.Decisions, Is.EqualTo(1), "the request and the wait of its task are one decision");
        });
    }

    [Test]
    public void A_request_no_task_asks_has_an_amber_panel_of_its_own()
    {
        var tasks = new[] { Work("a", WorkTaskStatus.Working, minutes: 5) };

        var content = FanContent.Of(tasks, Open(null));

        Assert.Multiple(() =>
        {
            Assert.That(content.Panels, Has.Count.EqualTo(2));
            Assert.That(content.Panels[0].Task, Is.Null);
            Assert.That(content.Panels[0].Amber, Is.True);
            Assert.That(content.Panels[0].Decision, Is.Not.Null);
            Assert.That(content.Tasks, Is.EqualTo(1));
            Assert.That(content.Decisions, Is.EqualTo(1));
        });
    }

    [Test]
    public void Beyond_five_panels_the_other_tasks_are_counted_and_the_task_that_asks_keeps_its_panel()
    {
        var tasks = Enumerable.Range(0, 8).Select(index => Work("t" + index, WorkTaskStatus.Working, minutes: index)).ToArray();

        var content = FanContent.Of(tasks, Open("t0"));

        Assert.Multiple(() =>
        {
            Assert.That(content.Panels, Has.Count.EqualTo(FanLayout.MostPanels));
            Assert.That(content.Panels[0].Task!.Id, Is.EqualTo("t0"), "the oldest task, first because its request waits");
            Assert.That(content.Panels[0].Amber, Is.True);
            Assert.That(content.Tasks, Is.EqualTo(8));
            Assert.That(content.Hidden, Is.EqualTo(3));
            Assert.That(content.Decisions, Is.EqualTo(1));
        });
    }

    [Test]
    public void Without_a_task_the_fan_is_empty()
    {
        var content = FanContent.Of([], null);
        Assert.Multiple(() =>
        {
            Assert.That(content.Panels, Is.Empty);
            Assert.That(content.Tasks, Is.Zero);
            Assert.That(content.Decisions, Is.Zero);
        });
    }

    [Test]
    public void The_orb_counts_the_tasks_that_work_not_the_ones_that_wait_or_ended_their_turn()
    {
        var tasks = new[]
        {
            Work("a", WorkTaskStatus.Working, minutes: 1),
            Work("b", WorkTaskStatus.Pending, minutes: 1),
            Work("c", WorkTaskStatus.WaitingApproval, minutes: 1),
            Work("d", WorkTaskStatus.Idle, minutes: 1),
            Work("e", WorkTaskStatus.Blocked, minutes: 1),
            Work("f", WorkTaskStatus.Done, minutes: 1),
            Work("a1", WorkTaskStatus.Working, minutes: 1, parent: "a"),
        };

        Assert.That(TaskCount.Working(tasks), Is.EqualTo(2));
    }

    private static WorkTask Work(string id, WorkTaskStatus status, int minutes, string? parent = null) => new(
        id,
        parent,
        "github.com/kazo-ca/jarvis",
        TaskKeys.Inbox,
        "task " + id,
        null,
        new TaskOperator(TaskOperator.ClaudeCode, "session-" + id),
        TaskSource.Harness,
        status,
        Start,
        Start.AddMinutes(-minutes),
        "/home/demo/jarvis");

    private static OpenConfirmation Open(string? task) => new(
        Guid.NewGuid(),
        new ConfirmationRequest(new ActionRequester(RequesterKind.Session, "jarvis"), "Write", null, JsonSerializer.Serialize(new { file_path = "a.md" }), ActionClass.Sensitive, ActionContext.None)
        {
            TaskId = task,
        },
        "Claude Code wants to write a.md.",
        false);

    private static async Task WaitAsync(Func<bool> reached)
    {
        for (var tries = 0; tries < 200 && !reached(); tries++)
        {
            await Task.Delay(10);
        }

        Assert.That(reached(), Is.True);
    }

    private sealed class Surface : ITaskFanSurface
    {
        public FanAnchor Anchor { get; set; } = FanAnchor.BottomRight;

        public bool Shown { get; private set; }

        public int Hides { get; private set; }

        public void Show() => Shown = true;

        public void Hide()
        {
            Shown = false;
            Hides++;
        }
    }
}
