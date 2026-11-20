using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The orb slice (work item stage1/6-orb): the seven looks on the demonstration page (US1.3), reduced
/// motion, the still image of the idle look, the badge, the progress ring, the fault on hover, and the click
/// that activates the assistant on the host page (US1.6).
/// </summary>
[TestFixture]
public sealed class OrbTests : PageTest
{
    private ILocator Orb => Page.Locator("#orb-demo .orb");

    private static readonly string[] Looks = ["Idle", "Listening", "Thinking", "Speaking", "LongTask", "Error", "MicrophoneMuted"];

    private async Task OpenDemoAsync()
    {
        await Page.GotoAsync(new Uri(WebHost.BaseAddress, "orb").ToString());
        await Expect(Page.Locator("#orb-demo")).ToHaveAttributeAsync("data-interactive", "true");

        // The script is attached once the motion attribute appears.
        await Expect(Orb).ToHaveAttributeAsync("data-motion", "full");
    }

    /// <summary>Number of ring rotations that run (Web Animations API, compositor only).</summary>
    private Task<int> RunningRotationsAsync() =>
        Page.EvaluateAsync<int>("() => document.getAnimations().filter(a => a.playState === 'running' && a.effect?.getComputedTiming().iterations === Infinity).length");

    /// <summary>Waits until <paramref name="expected"/> rotations run; the speed eases over about 0.6 s.</summary>
    private async Task ExpectRunningRotationsAsync(int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var running = await RunningRotationsAsync();
        while (running != expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            running = await RunningRotationsAsync();
        }

        Assert.That(running, Is.EqualTo(expected), "Ring rotations running.");
    }

    [Test]
    public async Task Each_look_is_shown_and_exposed()
    {
        await OpenDemoAsync();

        foreach (var look in Looks)
        {
            await Page.ClickAsync($"#look-{look}");
            await Expect(Orb).ToHaveAttributeAsync("data-look", look);
        }
    }

    [Test]
    public async Task Listening_rotates_the_rings_and_idle_is_a_still_image()
    {
        await OpenDemoAsync();

        await Page.ClickAsync("#look-Listening");
        await ExpectRunningRotationsAsync(3);

        // After the speed eases down (about 0.6 s), no animation runs at all.
        await Page.ClickAsync("#look-Idle");
        await ExpectRunningRotationsAsync(0);

        await Page.ClickAsync("#look-MicrophoneMuted");
        await Expect(Orb.Locator(".orb-label")).ToBeVisibleAsync();
        Assert.That(await RunningRotationsAsync(), Is.Zero);
    }

    [Test]
    public async Task The_animated_idle_look_is_only_an_option()
    {
        await OpenDemoAsync();
        await Page.ClickAsync("#look-Idle");
        await ExpectRunningRotationsAsync(0);

        await Page.CheckAsync("#demo-animate-idle");
        await ExpectRunningRotationsAsync(3);

        await Page.UncheckAsync("#demo-animate-idle");
        await ExpectRunningRotationsAsync(0);
    }

    [Test]
    public async Task Reduced_motion_of_the_system_stops_every_rotation()
    {
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await Page.GotoAsync(new Uri(WebHost.BaseAddress, "orb").ToString());
        await Expect(Page.Locator("#orb-demo")).ToHaveAttributeAsync("data-interactive", "true");
        await Expect(Orb).ToHaveAttributeAsync("data-motion", "reduced");

        await Page.ClickAsync("#look-Thinking");
        await Expect(Orb).ToHaveAttributeAsync("data-look", "Thinking");
        Assert.That(await RunningRotationsAsync(), Is.Zero);
    }

    [Test]
    public async Task The_reduced_motion_option_stops_every_rotation()
    {
        await OpenDemoAsync();
        await Page.ClickAsync("#look-Thinking");
        await ExpectRunningRotationsAsync(3);

        await Page.CheckAsync("#demo-reduced");
        await Expect(Orb).ToHaveAttributeAsync("data-motion", "reduced");
        await ExpectRunningRotationsAsync(0);
    }

    [Test]
    public async Task The_badge_and_the_progress_ring_draw_over_any_look()
    {
        await OpenDemoAsync();
        await Page.ClickAsync("#look-Speaking");
        await Expect(Orb.Locator(".orb-badge")).ToHaveCountAsync(0);
        await Expect(Orb.Locator(".orb-progress")).ToHaveCountAsync(0);

        await Page.FillAsync("#demo-decisions", "3");
        await Page.FillAsync("#demo-tasks", "1");

        await Expect(Orb.Locator(".orb-badge")).ToHaveTextAsync("3");
        await Expect(Orb.Locator(".orb-progress")).ToHaveAttributeAsync("data-progress", "0.4");

        await Page.UncheckAsync("#demo-progress-known");
        await Expect(Orb.Locator(".orb-progress")).ToHaveAttributeAsync("data-progress", "unknown");
        await Expect(Orb).ToHaveAttributeAsync("data-look", "Speaking");
    }

    [Test]
    public async Task The_error_shows_its_cause_on_hover()
    {
        await OpenDemoAsync();
        await Page.SelectOptionAsync("#demo-fault", "ModelUnreachable");
        await Page.ClickAsync("#look-Error");

        await Expect(Orb).ToHaveAttributeAsync("title", "Jarvis has a problem: the language model does not answer.");
        await ExpectRunningRotationsAsync(0);
    }

    [Test]
    public async Task A_click_on_the_orb_is_the_component_output()
    {
        await OpenDemoAsync();

        await Orb.ClickAsync();
        await Expect(Page.Locator("#demo-clicks")).ToHaveAttributeAsync("data-count", "1");
    }

    [Test]
    public async Task A_click_on_the_host_orb_starts_listening_then_cancels_it()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        var root = Page.Locator("#jarvis-root");
        await Expect(root).ToHaveAttributeAsync("data-interactive", "true");
        var orb = Page.Locator(".orb-frame .orb");
        await Expect(orb).ToHaveAttributeAsync("data-motion", "full");
        await Expect(orb).ToHaveAttributeAsync("data-look", "Idle");

        await orb.ClickAsync();
        await Expect(root).ToHaveAttributeAsync("data-phase", "Listening");
        await Expect(orb).ToHaveAttributeAsync("data-look", "Listening");

        await orb.ClickAsync();
        await Expect(root).ToHaveAttributeAsync("data-phase", "Idle");
        await Expect(orb).ToHaveAttributeAsync("data-look", "Idle");
    }
}
