using System.Text.Json;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Mcp.HomeAssistant;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools.Servers;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Mcp;

/// <summary>
/// The classification of Home Assistant (ADR 0018, section 5): a call is classed from the devices it aims at, read
/// from <c>GetLiveContext</c>, never from the name of its tool; a lock, an alarm or a garage door asks for a click,
/// a light does not; a target that is not found takes the highest class it could have; a script is an everyday
/// effect until the user marks it; and without the list of the devices nothing is classed.
/// </summary>
[TestFixture]
public sealed class HomeAssistantClassifierTests
{
    private const string Live =
        "Live Context: An overview of the areas and the devices in this smart home:\n"
        + "- names: Lampe du salon, Lumière télé\n  domain: light\n  state: 'off'\n  areas: Salon\n  attributes:\n    brightness: '0'\n"
        + "- names: Serrure de l'entrée\n  domain: lock\n  state: locked\n  areas: Entrée\n"
        + "- names: Porte du garage\n  domain: cover\n  state: closed\n  areas: Garage\n  attributes:\n    device_class: garage\n"
        + "- names: Store du bureau\n  domain: cover\n  state: open\n  areas: Bureau\n  attributes:\n    device_class: shade\n"
        + "- names: Prise du sapin\n  domain: switch\n  state: 'off'\n  areas: Salon\n"
        + "- names: 'Lampe d''appoint'\n  domain: light\n  state: 'on'\n  areas: Entrée\n";

    private static readonly JsonElement Target = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new { name = new { type = "string" }, area = new { type = "string" }, floor = new { type = "string" }, domain = new { type = "array" }, device_class = new { type = "array" } },
    });

    private static readonly JsonElement Nothing = JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });

    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;
    private FakeTimeProvider time = null!;
    private HomeAssistantClassifier classifier = null!;
    private List<string> asked = null!;
    private string? live;
    private string liveTool = "homeassistant__GetLiveContext";

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        classifier = new HomeAssistantClassifier(settings, time);
        asked = [];
        live = JsonSerializer.Serialize(new { success = true, result = Live });
        liveTool = "homeassistant__GetLiveContext";
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    private static ToolServerTool Tool(string name, bool targets = true, bool readOnly = false) => new(name, "Does something.", targets ? Target : Nothing, readOnly);

    private ValueTask<ActionClass> ClassAsync(string tool, string arguments = "{}", bool targets = true, string server = "maison", bool readOnly = false)
    {
        using var document = JsonDocument.Parse(arguments);
        var call = new ToolServerCall(server, Tool(tool, targets, readOnly), document.RootElement.Clone(), (name, _, _) =>
        {
            asked.Add(name);
            return ValueTask.FromResult(name == liveTool && live is not null ? new ToolServerResult(live, IsError: false) : new ToolServerResult("Unknown tool", IsError: true));
        });
        return classifier.ClassifyAsync(call);
    }

    private ToolServerCall CallOf(string tool, string arguments)
    {
        using var document = JsonDocument.Parse(arguments);
        return new ToolServerCall("maison", Tool(tool), document.RootElement.Clone(), (name, _, _) =>
        {
            asked.Add(name);
            return ValueTask.FromResult(name == liveTool && live is not null ? new ToolServerResult(live, IsError: false) : new ToolServerResult("Unknown tool", IsError: true));
        });
    }

    private async Task<string?> ResolvedNameAsync(string arguments, string tool = "intent__HassTurnOff")
    {
        var resolved = await classifier.ResolveAsync(CallOf(tool, arguments));
        return resolved.TryGetProperty("name", out var name) ? name.GetString() : null;
    }

    /// <summary>
    /// Seen at the acceptance: « Éteins la lumière articulée » gave the name « lumière articulée » for the light
    /// named « Articulé », and Home Assistant found nothing. The name is written as Home Assistant writes it when
    /// one device only carries the words said.
    /// </summary>
    [Test]
    public async Task A_name_said_with_its_kind_and_another_ending_is_written_as_home_assistant_writes_it()
    {
        live = JsonSerializer.Serialize(new { success = true, result = Live + "- names: Articulé\n  domain: light\n  state: 'on'\n  areas: Salon\n" });

        Assert.Multiple(async () =>
        {
            Assert.That(await ResolvedNameAsync("""{"name":"lumière articulée","domain":["light"]}"""), Is.EqualTo("Articulé"));
            Assert.That(await ResolvedNameAsync("""{"name":"la lampe articulé"}"""), Is.EqualTo("Articulé"));
            Assert.That(await ResolvedNameAsync("""{"name":"ARTICULE"}"""), Is.EqualTo("Articulé"));
            Assert.That(await ResolvedNameAsync("""{"name":"lampe du salon"}"""), Is.EqualTo("lampe du salon"), "an exact name is left alone");
            Assert.That(await ResolvedNameAsync("""{"name":"la lumière de la télé","domain":["light"]}"""), Is.EqualTo("Lumière télé"));
            Assert.That(await ResolvedNameAsync("""{"name":"prises du sapin"}"""), Is.EqualTo("Prise du sapin"));
        });

        // The other arguments are kept as they were.
        var resolved = await classifier.ResolveAsync(CallOf("intent__HassTurnOff", """{"name":"lumière articulée","domain":["light"]}"""));
        Assert.That(resolved.GetProperty("domain")[0].GetString(), Is.EqualTo("light"));
    }

    [Test]
    public async Task A_name_that_fits_no_device_or_several_stays_as_the_model_wrote_it()
    {
        live = JsonSerializer.Serialize(new { success = true, result = Live + "- names: Lampe salon\n  domain: light\n  state: 'on'\n  areas: Cuisine\n" });

        Assert.Multiple(async () =>
        {
            Assert.That(await ResolvedNameAsync("""{"name":"le truc"}"""), Is.EqualTo("le truc"));
            Assert.That(await ResolvedNameAsync("""{"name":"lumière du bureau","domain":["light"]}"""), Is.EqualTo("lumière du bureau"), "the shade of the office is not a light");
            Assert.That(await ResolvedNameAsync("""{"name":"lampes salon"}"""), Is.EqualTo("lampes salon"), "two lights carry these words");
            Assert.That(await ResolvedNameAsync("""{"name":"lampes salon","area":"Cuisine"}"""), Is.EqualTo("Lampe salon"), "the area leaves one");
        });
    }

    /// <summary>An approximate name never reaches a lock or a garage door: those are named as they are written, or not at all.</summary>
    [Test]
    public async Task An_approximate_name_never_reaches_a_security_device()
    {
        await WriteAsync(new HomeAssistantOptions { SecurityEntities = ["Prise du sapin"] });

        Assert.Multiple(async () =>
        {
            Assert.That(await ResolvedNameAsync("""{"name":"serrures de l'entrée"}"""), Is.EqualTo("serrures de l'entrée"));
            Assert.That(await ResolvedNameAsync("""{"name":"portes du garage"}"""), Is.EqualTo("portes du garage"));
            Assert.That(await ResolvedNameAsync("""{"name":"prises du sapin"}"""), Is.EqualTo("prises du sapin"), "marked by the user");
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"name":"serrures de l'entrée"}"""), Is.EqualTo(ActionClass.Critical), "not found: the highest class it could have");
        });
    }

    [Test]
    public async Task After_a_name_that_is_not_found_the_model_is_told_the_names_it_can_use()
    {
        var hint = await classifier.HintAsync(CallOf("intent__HassTurnOff", """{"name":"le truc","domain":["light"]}"""));
        var known = await classifier.HintAsync(CallOf("intent__HassTurnOff", """{"name":"Lampe du salon"}"""));
        var scripted = await classifier.HintAsync(CallOf("script__depart", """{"name":"le truc"}"""));

        Assert.Multiple(() =>
        {
            Assert.That(hint, Does.Contain("\"Lampe du salon\" (Salon)").And.Contain("\"Lampe d'appoint\" (Entrée)").And.Contain("le truc"));
            Assert.That(hint, Does.Not.Contain("Serrure").And.Not.Contain("Prise"), "only the lights were asked for");
            Assert.That(known, Is.Null, "the name exists: the failure is about something else");
            Assert.That(scripted, Is.Null);
        });
    }

    private Task WriteAsync(HomeAssistantOptions options) => settings.WriteAsync(HomeAssistantOptions.Section, options).AsTask();

    [Test]
    public void The_answer_of_Home_Assistant_is_read_entity_by_entity()
    {
        var entities = LiveContext.Parse(JsonSerializer.Serialize(new { success = true, result = Live }));

        Assert.Multiple(() =>
        {
            Assert.That(entities, Has.Count.EqualTo(6));
            Assert.That(entities[0].Names, Is.EqualTo(new[] { "Lampe du salon", "Lumière télé" }));
            Assert.That(entities[0].Domain, Is.EqualTo("light"));
            Assert.That(entities[0].Areas, Is.EqualTo(new[] { "Salon" }));
            Assert.That(entities[0].DeviceClass, Is.Null);
            Assert.That(entities[2].DeviceClass, Is.EqualTo("garage"));
            Assert.That(entities[5].Names, Is.EqualTo(new[] { "Lampe d'appoint" }), "A quoted name of YAML.");
            Assert.That(entities[0].IsNamed("lampe du SALON"), Is.True);
            Assert.That(LiveContext.Parse(Live), Has.Count.EqualTo(6), "The text alone, without its JSON envelope.");
            Assert.That(LiveContext.Parse("<html>Sign in</html>"), Is.Empty);
            Assert.That(LiveContext.Parse("- names: Sans domaine\n  state: 'on'\n"), Is.Empty, "An entry without a domain cannot be classed.");
        });
    }

    [Test]
    public void The_lowest_class_of_a_tool_comes_from_what_it_is()
    {
        Assert.Multiple(() =>
        {
            Assert.That(classifier.Kind, Is.EqualTo(ToolServerKinds.HomeAssistant));
            Assert.That(classifier.LowestClass(Tool("homeassistant__GetLiveContext", targets: false)), Is.EqualTo(ActionClass.Read));
            Assert.That(classifier.LowestClass(Tool("llm__GetDateTime", targets: false)), Is.EqualTo(ActionClass.Read));
            Assert.That(classifier.LowestClass(Tool("todo__get_items", targets: false)), Is.EqualTo(ActionClass.Read));
            Assert.That(classifier.LowestClass(Tool("intent__HassTurnOff")), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(classifier.LowestClass(Tool("script__nuit", targets: false)), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(classifier.LowestClass(Tool("assist_satellite__HassBroadcast", targets: false)), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(classifier.LowestClass(Tool("todo__HassListAddItem", targets: false)), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(classifier.LowestClass(Tool("something__New", targets: false)), Is.EqualTo(ActionClass.Sensitive), "A tool nobody knows asks first.");
        });
    }

    [Test]
    public async Task A_light_is_an_everyday_effect_and_a_lock_asks_for_a_click_with_the_same_tool()
    {
        Assert.Multiple(async () =>
        {
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon"}"""), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"name":"lumière télé"}"""), Is.EqualTo(ActionClass.EverydayEffect), "An alias, whatever the case.");
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"name":"Serrure de l'entrée"}"""), Is.EqualTo(ActionClass.Critical), "HassTurnOff unlocks a lock.");
            Assert.That(await ClassAsync("intent__HassTurnOn", """{"name":"Porte du garage"}"""), Is.EqualTo(ActionClass.Critical));
            Assert.That(await ClassAsync("intent__HassTurnOn", """{"name":"Store du bureau"}"""), Is.EqualTo(ActionClass.EverydayEffect), "A shade is a cover, not a door.");
        });
        Assert.That(asked, Has.All.EqualTo("homeassistant__GetLiveContext"));
    }

    [Test]
    public async Task A_room_takes_the_highest_class_of_what_it_holds()
    {
        Assert.Multiple(async () =>
        {
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"area":"Salon"}"""), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"area":"Entrée"}"""), Is.EqualTo(ActionClass.Critical), "The lock is in the room.");
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"area":"Entrée","domain":["light"]}"""), Is.EqualTo(ActionClass.EverydayEffect), "Only the lights of the room.");
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"area":"Entrée","domain":"light"}"""), Is.EqualTo(ActionClass.EverydayEffect), "A filter written as one text.");
            Assert.That(await ClassAsync("intent__HassTurnOn", """{"device_class":["garage"]}"""), Is.EqualTo(ActionClass.Critical));
            Assert.That(await ClassAsync("intent__HassTurnOn", """{"domain":["cover"],"device_class":["shade"]}"""), Is.EqualTo(ActionClass.EverydayEffect));
        });
    }

    [Test]
    public async Task A_target_that_is_not_found_takes_the_highest_class_it_could_have()
    {
        Assert.Multiple(async () =>
        {
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"name":"la porte de devant"}"""), Is.EqualTo(ActionClass.Critical), "It could be the lock.");
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"name":"la porte de devant","domain":["light"]}"""), Is.EqualTo(ActionClass.EverydayEffect), "It can only be a light.");
            Assert.That(await ClassAsync("intent__HassTurnOff", "{}"), Is.EqualTo(ActionClass.Critical), "No target at all.");
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"floor":"Étage","name":"Lampe du salon"}"""), Is.EqualTo(ActionClass.Critical), "The list does not give the floors.");
            Assert.That(await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon","area":"Garage"}"""), Is.EqualTo(ActionClass.Critical), "The name and the room do not agree.");
        });
    }

    [Test]
    public async Task An_entity_the_user_marked_asks_for_a_click()
    {
        Assert.That(await ClassAsync("intent__HassTurnOn", """{"name":"Prise du sapin"}"""), Is.EqualTo(ActionClass.EverydayEffect));

        await WriteAsync(new HomeAssistantOptions { SecurityEntities = ["prise du sapin"] });

        Assert.Multiple(async () =>
        {
            Assert.That(await ClassAsync("intent__HassTurnOn", """{"name":"Prise du sapin"}"""), Is.EqualTo(ActionClass.Critical));
            Assert.That(await ClassAsync("intent__HassTurnOn", """{"area":"Salon"}"""), Is.EqualTo(ActionClass.Critical), "The room holds it.");
            Assert.That(await ClassAsync("intent__HassTurnOn", """{"name":"Lampe du salon"}"""), Is.EqualTo(ActionClass.EverydayEffect));
        });
    }

    [Test]
    public async Task A_script_is_an_everyday_effect_until_the_user_marks_it()
    {
        Assert.Multiple(async () =>
        {
            Assert.That(await ClassAsync("script__depart", targets: false), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(await ClassAsync("intent_script__NousQuittons", targets: false), Is.EqualTo(ActionClass.EverydayEffect));
        });

        await WriteAsync(new HomeAssistantOptions { SecurityScripts = ["maison__script__depart", "intent_script__NousQuittons"] });

        Assert.Multiple(async () =>
        {
            Assert.That(await ClassAsync("script__depart", targets: false), Is.EqualTo(ActionClass.Critical), "Marked under its name in the registry, prefixed by its server.");
            Assert.That(await ClassAsync("intent_script__NousQuittons", targets: false), Is.EqualTo(ActionClass.Critical));
            Assert.That(await ClassAsync("script__soiree", targets: false), Is.EqualTo(ActionClass.EverydayEffect));
        });
        Assert.That(asked, Is.Empty, "A script does not say what it actuates: the server is not asked.");
    }

    [Test]
    public async Task The_other_tools_follow_the_known_list()
    {
        Assert.Multiple(async () =>
        {
            Assert.That(await ClassAsync("todo__get_items", targets: false), Is.EqualTo(ActionClass.Read));
            Assert.That(await ClassAsync("anything", targets: false, readOnly: true), Is.EqualTo(ActionClass.Read));
            Assert.That(await ClassAsync("intent__HassCancelAllTimers", targets: false), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(await ClassAsync("something__New", targets: false), Is.EqualTo(ActionClass.Sensitive));
        });
        Assert.That(asked, Is.Empty);
    }

    [Test]
    public void Without_the_exposed_entities_nothing_is_classed()
    {
        live = null;
        Assert.That(async () => await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon"}"""), Throws.InvalidOperationException);
        Assert.That(asked, Is.EqualTo(new[] { "homeassistant__GetLiveContext", "GetLiveContext" }), "Both names of the tool are tried.");

        live = "<html>Sign in</html>";
        Assert.That(async () => await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon"}"""), Throws.InvalidOperationException, "An answer nothing is read from is not an empty house.");
    }

    [Test]
    public async Task The_older_name_of_the_tool_is_asked_too()
    {
        liveTool = "GetLiveContext";
        Assert.That(await ClassAsync("HassTurnOff", """{"name":"Serrure de l'entrée"}"""), Is.EqualTo(ActionClass.Critical));
    }

    [Test]
    public async Task The_exposed_entities_are_kept_a_few_seconds_per_server()
    {
        await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon"}""");
        await ClassAsync("intent__HassTurnOff", """{"name":"Serrure de l'entrée"}""");
        Assert.That(asked, Has.Count.EqualTo(1), "Read once for both calls.");

        await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon"}""", server: "chalet");
        Assert.That(asked, Has.Count.EqualTo(2), "Another server has other devices.");

        time.Advance(TimeSpan.FromSeconds(6));
        await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon"}""");
        Assert.That(asked, Has.Count.EqualTo(3), "Read again after 5 seconds.");

        await WriteAsync(new HomeAssistantOptions { ContextSeconds = 0 });
        await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon"}""");
        await ClassAsync("intent__HassTurnOff", """{"name":"Lampe du salon"}""");
        Assert.That(asked, Has.Count.EqualTo(5), "0 reads at every call.");
    }

    [Test]
    public void The_settings_refuse_lists_that_cannot_be_used()
    {
        Assert.Multiple(async () =>
        {
            Assert.That((await settings.WriteAsync(HomeAssistantOptions.Section, new HomeAssistantOptions { SecurityEntities = [" "] })).Outcome, Is.Not.EqualTo(SettingsWriteOutcome.Saved));
            Assert.That((await settings.WriteAsync(HomeAssistantOptions.Section, new HomeAssistantOptions { ContextSeconds = 61 })).Outcome, Is.Not.EqualTo(SettingsWriteOutcome.Saved));
            Assert.That((await settings.WriteAsync(HomeAssistantOptions.Section, new HomeAssistantOptions { SecurityScripts = ["script__nuit"], ContextSeconds = 0 })).Outcome, Is.EqualTo(SettingsWriteOutcome.Saved));
        });
    }
}
