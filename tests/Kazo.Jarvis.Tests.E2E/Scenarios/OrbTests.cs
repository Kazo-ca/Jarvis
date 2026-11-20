using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The orb slice (work items stage1/6-orb and stage1/7-orb-high-quality): both versions and the seven looks
/// on the demonstration page (US1.3), reduced motion, the still image of the idle look, the badge, the
/// progress ring, the fault on hover, the "orb quality" setting applied without a restart (ADR 0011,
/// section 1), and the click that activates the assistant on the host page (US1.6).
/// </summary>
[TestFixture]
public sealed class OrbTests : PageTest
{
    private ILocator Orb => Page.Locator("#orb-demo .orb");

    private static readonly string[] Looks = ["Idle", "Listening", "Thinking", "Speaking", "LongTask", "Error", "MicrophoneMuted"];

    /// <summary>Ring rotations of the light version: three rings.</summary>
    private const int LightRotations = 3;

    /// <summary>Rotations of the high-quality version: five rings, the gyroscope's turn and its rocking.</summary>
    private const int HighQualityRotations = 7;

    private async Task OpenDemoAsync(string quality = "Light")
    {
        await Page.GotoAsync(new Uri(WebHost.BaseAddress, "orb").ToString());
        await Expect(Page.Locator("#orb-demo")).ToHaveAttributeAsync("data-interactive", "true");

        // The demonstration opens on the high quality, the default version (ADR 0011, section 1).
        await Expect(Orb).ToHaveAttributeAsync("data-quality", "High");
        if (quality != "High")
        {
            await Page.ClickAsync($"#quality-{quality}");
            await Expect(Orb).ToHaveAttributeAsync("data-quality", quality);
        }

        // The script is attached once the motion attribute appears.
        await Expect(Orb).ToHaveAttributeAsync("data-motion", "full");
    }

    /// <summary>Waits until the canvas loop of the high-quality version is <paramref name="expected"/>.</summary>
    private Task ExpectLoopAsync(string expected) =>
        Expect(Orb.Locator(".hq")).ToHaveAttributeAsync("data-loop", expected, new() { Timeout = 5000 });

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
        await ExpectRunningRotationsAsync(LightRotations);

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
        await ExpectRunningRotationsAsync(LightRotations);

        await Page.UncheckAsync("#demo-animate-idle");
        await ExpectRunningRotationsAsync(0);
    }

    [Test]
    public async Task Reduced_motion_of_the_system_stops_every_rotation()
    {
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await Page.GotoAsync(new Uri(WebHost.BaseAddress, "orb").ToString());
        await Expect(Page.Locator("#orb-demo")).ToHaveAttributeAsync("data-interactive", "true");
        await Page.ClickAsync("#quality-Light");
        await Expect(Orb).ToHaveAttributeAsync("data-quality", "Light");
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
        await ExpectRunningRotationsAsync(LightRotations);

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

    [Test]
    public async Task High_quality_turns_its_rings_and_draws_while_listening_then_stops_in_idle()
    {
        await OpenDemoAsync("High");

        await Page.ClickAsync("#look-Listening");
        await ExpectRunningRotationsAsync(HighQualityRotations);
        await ExpectLoopAsync("running");

        // The canvas is drawn at 30 images per second at most (ADR 0005, section 5).
        await Expect(Orb.Locator("canvas.hq-sphere")).ToHaveCountAsync(1);

        // Idle: after the speed eases down and the color fades, nothing runs, not even the canvas loop.
        await Page.ClickAsync("#look-Idle");
        await ExpectRunningRotationsAsync(0);
        await ExpectLoopAsync("stopped");

        foreach (var still in new[] { "MicrophoneMuted", "Error" })
        {
            await Page.ClickAsync($"#look-{still}");
            await Expect(Orb).ToHaveAttributeAsync("data-look", still);
            await ExpectLoopAsync("stopped");
            Assert.That(await RunningRotationsAsync(), Is.Zero, still);
        }
    }

    [Test]
    public async Task High_quality_speaking_follows_the_voice_by_light_without_pulses()
    {
        await OpenDemoAsync("High");
        await Page.ClickAsync("#look-Speaking");
        await ExpectRunningRotationsAsync(HighQualityRotations);

        // The light version's random ring pulses are short, finite animations; the high quality has none.
        await Page.WaitForTimeoutAsync(800);
        var finite = await Page.EvaluateAsync<int>("() => document.getAnimations().filter(a => a.effect?.getComputedTiming().iterations !== Infinity && a.playState === 'running').length");
        Assert.That(finite, Is.Zero);
    }

    [Test]
    public async Task The_reduced_motion_option_stops_the_high_quality_rotations()
    {
        await OpenDemoAsync("High");
        await Page.ClickAsync("#look-Thinking");
        await ExpectRunningRotationsAsync(HighQualityRotations);

        await Page.CheckAsync("#demo-reduced");
        await Expect(Orb).ToHaveAttributeAsync("data-motion", "reduced");
        await ExpectRunningRotationsAsync(0);
        await ExpectLoopAsync("stopped");
    }

    [Test]
    public async Task Changing_the_version_swaps_the_drawing_in_place()
    {
        await OpenDemoAsync("High");
        await Page.ClickAsync("#look-Listening");
        await Expect(Orb.Locator(".hq")).ToHaveCountAsync(1);
        await ExpectRunningRotationsAsync(HighQualityRotations);

        await Page.ClickAsync("#quality-Light");
        await Expect(Orb.Locator(".hq")).ToHaveCountAsync(0);
        await Expect(Orb.Locator(".orb-ring")).ToHaveCountAsync(3);
        await ExpectRunningRotationsAsync(LightRotations);

        await Page.ClickAsync("#quality-High");
        await Expect(Orb.Locator(".hq")).ToHaveCountAsync(1);
        await ExpectRunningRotationsAsync(HighQualityRotations);
    }

    [Test]
    public async Task The_orb_quality_setting_applies_to_the_host_orb_without_reloading()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Page.Locator("#jarvis-root")).ToHaveAttributeAsync("data-interactive", "true");
        var orb = Page.Locator(".orb-frame .orb");
        await Expect(orb).ToHaveAttributeAsync("data-quality", "High");
        await Page.EvaluateAsync("() => window.notReloaded = true");

        var settings = Page.Locator("[data-dialog-key='Settings.Window']");
        await Page.ClickAsync("#open-settings");
        await settings.Locator("[data-settings-page='Assistant.Orb']").ClickAsync();
        await Expect(settings.Locator("input[data-orb-quality='High']")).ToBeCheckedAsync();

        try
        {
            await settings.Locator("input[data-orb-quality='Light']").CheckAsync();
            await Expect(orb).ToHaveAttributeAsync("data-quality", "Light");
            await Expect(orb.Locator(".orb-ring")).ToHaveCountAsync(3);
            Assert.That(await Page.EvaluateAsync<bool>("() => window.notReloaded === true"), Is.True);
        }
        finally
        {
            // The Web host keeps its settings for the whole run: back to the default for the other tests.
            await settings.Locator("input[data-orb-quality='High']").CheckAsync();
            await Expect(orb).ToHaveAttributeAsync("data-quality", "High");
            await Page.ClickAsync("[data-dialog-key='Settings.Window'] .dialog-close");
        }
    }
}
