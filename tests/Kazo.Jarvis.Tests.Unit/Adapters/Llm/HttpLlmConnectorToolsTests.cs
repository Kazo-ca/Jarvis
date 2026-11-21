using System.Text.Json;
using Kazo.Jarvis.Adapters.Llm;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Llm;

/// <content>
/// The tool format of each provider (ADR 0007, sections 3 and 4): the declared tools are sent, a call arrives as a
/// <see cref="FunctionCallContent"/> and is never run by the adapter, a result goes back in the format of the
/// provider, and the local adapters accept a call written in the text.
/// </content>
public sealed partial class HttpLlmConnectorTests
{
    private static readonly JsonElement ExpressionSchema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new { expression = new { type = "string" } },
        required = new[] { "expression" },
    });

    private static ChatOptions WithTools() => new()
    {
        Tools =
        [
            AIFunctionFactory.CreateDeclaration("get_datetime", "Date et heure actuelles.", JsonSerializer.SerializeToElement(new { type = "object", properties = new { } })),
            AIFunctionFactory.CreateDeclaration("calculate", "Évalue une expression arithmétique.", ExpressionSchema),
        ],
    };

    /// <summary>A turn after one round: the request, the call the model made, its result.</summary>
    private static List<ChatMessage> AfterACall() =>
    [
        new(ChatRole.User, "Combien font 12 fois 7 ?"),
        new(ChatRole.Assistant, [new TextContent("Un instant."), new FunctionCallContent("call_1", "calculate", new Dictionary<string, object?> { ["expression"] = "12*7" })]),
        new(ChatRole.Tool, [new FunctionResultContent("call_1", "84")]),
    ];

    private static async Task<(string Text, List<FunctionCallContent> Calls, ChatFinishReason? Finish)> ReadAsync(IChatClient client, IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var text = string.Empty;
        var calls = new List<FunctionCallContent>();
        ChatFinishReason? finish = null;
        await foreach (var update in client.GetStreamingResponseAsync(messages, options))
        {
            text += update.Text;
            calls.AddRange(update.Contents.OfType<FunctionCallContent>());
            finish = update.FinishReason ?? finish;
        }

        return (text, calls, finish);
    }

    private static string? Argument(FunctionCallContent call, string name) =>
        call.Arguments is not null && call.Arguments.TryGetValue(name, out var value) && value is JsonElement json ? json.GetString() : null;

    [Test]
    public async Task Ollama_sends_the_tools_and_reads_a_call()
    {
        const string stream = """
            {"model":"qwen3:14b","message":{"role":"assistant","content":"","tool_calls":[{"id":"call_abc","function":{"index":0,"name":"calculate","arguments":{"expression":"12*7"}}}]},"done":false}
            {"model":"qwen3:14b","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":200,"eval_count":20}
            """;
        var (connector, handler, _) = Create((_, _) => StubHandler.Stream(stream));
        using var client = connector.CreateClient(Ollama());

        var (text, calls, finish) = await ReadAsync(client, Question(), WithTools());

        using var body = JsonDocument.Parse(handler.Received[0].Body);
        var tools = body.RootElement.GetProperty("tools");
        Assert.Multiple(() =>
        {
            Assert.That(tools.GetArrayLength(), Is.EqualTo(2));
            Assert.That(tools[1].GetProperty("type").GetString(), Is.EqualTo("function"));
            Assert.That(tools[1].GetProperty("function").GetProperty("name").GetString(), Is.EqualTo("calculate"));
            Assert.That(tools[1].GetProperty("function").GetProperty("description").GetString(), Is.EqualTo("Évalue une expression arithmétique."));
            Assert.That(tools[1].GetProperty("function").GetProperty("parameters").GetProperty("required")[0].GetString(), Is.EqualTo("expression"));

            Assert.That(text, Is.Empty);
            Assert.That(calls.Single().Name, Is.EqualTo("calculate"));
            Assert.That(calls.Single().CallId, Is.EqualTo("call_abc"));
            Assert.That(Argument(calls.Single(), "expression"), Is.EqualTo("12*7"));
            Assert.That(finish, Is.EqualTo(ChatFinishReason.ToolCalls));
        });
    }

    [Test]
    public async Task Ollama_sends_no_tools_field_without_tools_and_returns_the_result_of_a_call()
    {
        var (connector, handler, _) = Create((_, _) => StubHandler.Stream("""{"message":{"content":"Quatre-vingt-quatre."},"done":true}"""));
        using var client = connector.CreateClient(Ollama());

        await ReadAsync(client, AfterACall(), null);

        using var body = JsonDocument.Parse(handler.Received[0].Body);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Multiple(() =>
        {
            Assert.That(body.RootElement.TryGetProperty("tools", out _), Is.False);
            Assert.That(messages[1].GetProperty("role").GetString(), Is.EqualTo("assistant"));
            Assert.That(messages[1].GetProperty("content").GetString(), Is.EqualTo("Un instant."));
            Assert.That(messages[1].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("name").GetString(), Is.EqualTo("calculate"));
            Assert.That(messages[1].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("arguments").GetProperty("expression").GetString(), Is.EqualTo("12*7"), "Ollama takes the arguments as an object.");
            Assert.That(messages[2].GetProperty("role").GetString(), Is.EqualTo("tool"));
            Assert.That(messages[2].GetProperty("content").GetString(), Is.EqualTo("84"));
            Assert.That(messages[2].GetProperty("tool_name").GetString(), Is.EqualTo("calculate"));
        });
    }

    [TestCase("""{"name":"calculate","arguments":{"expression":"12*7"}}""")]
    [TestCase("""{"name":"calculate","parameters":{"expression":"12*7"}}""")]
    [TestCase("```json\n{\"name\": \"calculate\", \"arguments\": {\"expression\": \"12*7\"}}\n```")]
    public async Task A_local_model_may_write_its_call_in_the_text(string written)
    {
        // Sent in two pieces: the answer starts with "{" (or a code fence) and is held back until it ends.
        var half = written.Length / 2;
        var stream =
            $$"""{"message":{"content":{{JsonSerializer.Serialize(written[..half])}}},"done":false}""" + "\n" +
            $$"""{"message":{"content":{{JsonSerializer.Serialize(written[half..])}}},"done":false}""" + "\n" +
            """{"message":{"content":""},"done":true,"done_reason":"stop"}""";
        var (connector, _, _) = Create((_, _) => StubHandler.Stream(stream));
        using var client = connector.CreateClient(Ollama());

        var (text, calls, finish) = await ReadAsync(client, Question(), WithTools());

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.Empty, "A tool call is never said aloud.");
            Assert.That(calls.Single().Name, Is.EqualTo("calculate"));
            Assert.That(Argument(calls.Single(), "expression"), Is.EqualTo("12*7"));
            Assert.That(finish, Is.EqualTo(ChatFinishReason.ToolCalls));
        });
    }

    [TestCase("""{"name":"open_the_pod_bay_doors","arguments":{}}""", Description = "Not a declared tool.")]
    [TestCase("""{"name":"calculate","arguments":{"expression":"1"}} Voilà.""", Description = "Not the whole answer.")]
    [TestCase("""{"name":"calculate","arguments":"12*7"}""", Description = "Arguments that are not an object.")]
    [TestCase("""{ pas du JSON""")]
    public async Task Text_that_is_not_exactly_one_call_of_a_declared_tool_stays_text(string written)
    {
        var stream = $$"""{"message":{"content":{{JsonSerializer.Serialize(written)}}},"done":false}""" + "\n" + """{"message":{"content":""},"done":true}""";
        var (connector, _, _) = Create((_, _) => StubHandler.Stream(stream));
        using var client = connector.CreateClient(Ollama());

        var (text, calls, finish) = await ReadAsync(client, Question(), WithTools());

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo(written), "Held back, then given whole.");
            Assert.That(calls, Is.Empty);
            Assert.That(finish, Is.EqualTo(ChatFinishReason.Stop));
        });
    }

    [Test]
    public async Task The_tolerant_parser_is_off_without_tools_and_when_the_provider_turns_it_off()
    {
        const string written = """{"name":"calculate","arguments":{"expression":"12*7"}}""";
        var stream = $$"""{"message":{"content":{{JsonSerializer.Serialize(written)}}},"done":true}""";
        var (connector, _, _) = Create((_, _) => StubHandler.Stream(stream));
        using var plain = connector.CreateClient(Ollama());
        using var strict = connector.CreateClient(Ollama(tuning: new LlmTuning(TolerantToolCalls: false)));

        var withoutTools = await ReadAsync(plain, Question(), null);
        var turnedOff = await ReadAsync(strict, Question(), WithTools());

        Assert.Multiple(() =>
        {
            Assert.That(withoutTools.Text, Is.EqualTo(written));
            Assert.That(withoutTools.Calls, Is.Empty);
            Assert.That(turnedOff.Text, Is.EqualTo(written));
            Assert.That(turnedOff.Calls, Is.Empty);
        });
    }

    [Test]
    public async Task An_ordinary_answer_is_not_held_back_when_tools_are_declared()
    {
        const string stream = """
            {"message":{"content":"  "},"done":false}
            {"message":{"content":"Il est "},"done":false}
            {"message":{"content":"midi."},"done":true}
            """;
        var (connector, _, _) = Create((_, _) => StubHandler.Stream(stream));
        using var client = connector.CreateClient(Ollama());

        var pieces = new List<string>();
        await foreach (var update in client.GetStreamingResponseAsync(Question(), WithTools()))
        {
            if (update.Text.Length > 0)
            {
                pieces.Add(update.Text);
            }
        }

        Assert.That(pieces, Is.EqualTo(new[] { "  Il est ", "midi." }), "Only the leading white space waits for the first character.");
    }

    [Test]
    public async Task An_openai_compatible_server_streams_a_call_in_pieces()
    {
        const string stream = """
            data: {"model":"default","choices":[{"delta":{"role":"assistant","tool_calls":[{"index":0,"id":"call_9","type":"function","function":{"name":"calculate","arguments":""}}]}}]}

            data: {"model":"default","choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"expres"}}]}}]}

            data: {"model":"default","choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"sion\":\"12*7\"}"}},{"index":1,"id":"call_10","function":{"name":"get_datetime","arguments":"{}"}}]}}]}

            data: {"model":"default","choices":[{"delta":{},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":50,"completion_tokens":9}}

            data: [DONE]
            """;
        var (connector, handler, _) = Create((_, _) => StubHandler.Stream(stream));
        using var client = connector.CreateClient(Server(LlmProviderKind.OpenAiCompatible, "http://127.0.0.1:8080", "default"));

        var (text, calls, finish) = await ReadAsync(client, AfterACall(), WithTools());

        using var body = JsonDocument.Parse(handler.Received[0].Body);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Multiple(() =>
        {
            Assert.That(body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString(), Is.EqualTo("get_datetime"));
            Assert.That(messages[1].GetProperty("tool_calls")[0].GetProperty("id").GetString(), Is.EqualTo("call_1"));
            Assert.That(messages[1].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("arguments").GetString(), Is.EqualTo("""{"expression":"12*7"}"""), "The arguments are JSON text here.");
            Assert.That(messages[2].GetProperty("role").GetString(), Is.EqualTo("tool"));
            Assert.That(messages[2].GetProperty("tool_call_id").GetString(), Is.EqualTo("call_1"));
            Assert.That(messages[2].GetProperty("content").GetString(), Is.EqualTo("84"));

            Assert.That(text, Is.Empty);
            Assert.That(calls.Select(call => (call.CallId, call.Name)), Is.EqualTo(new[] { ("call_9", "calculate"), ("call_10", "get_datetime") }));
            Assert.That(Argument(calls[0], "expression"), Is.EqualTo("12*7"));
            Assert.That(finish, Is.EqualTo(ChatFinishReason.ToolCalls));
        });
    }

    [Test]
    public async Task Arguments_that_are_not_json_mark_the_call_instead_of_failing_the_answer()
    {
        const string stream = """
            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_9","function":{"name":"calculate","arguments":"{\"expression\": 12*"}}]}}]}

            data: [DONE]
            """;
        var (connector, _, _) = Create((_, _) => StubHandler.Stream(stream));
        using var client = connector.CreateClient(Server(LlmProviderKind.OpenAiCompatible, "http://127.0.0.1:8080", "default"));

        var (_, calls, _) = await ReadAsync(client, Question(), WithTools());

        Assert.Multiple(() =>
        {
            Assert.That(calls.Single().Name, Is.EqualTo("calculate"));
            Assert.That(calls.Single().Exception, Is.InstanceOf<FormatException>(), "The decision point denies a call it cannot read.");
        });
    }

    [Test]
    public async Task Anthropic_sends_the_tools_reads_a_tool_use_block_and_returns_results_in_one_user_message()
    {
        const string stream = """
            data: {"type":"message_start","message":{"model":"claude-opus-5-5","usage":{"input_tokens":80}}}

            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Je calcule."}}

            data: {"type":"content_block_stop","index":0}

            data: {"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_1","name":"calculate","input":{}}}

            data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"expression\":"}}

            data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"\"12*7\"}"}}

            data: {"type":"content_block_stop","index":1}

            data: {"type":"message_delta","delta":{"stop_reason":"tool_use"},"usage":{"output_tokens":30}}

            data: {"type":"message_stop"}
            """;
        var (connector, handler, vault) = Create((_, _) => StubHandler.Stream(stream));
        await vault.SetAsync(KeyRef, new Kazo.Jarvis.Kernel.Secrets.SecretValue("sk-test"));
        using var client = connector.CreateClient(Server(LlmProviderKind.Anthropic, "https://api.anthropic.com", "claude-opus-5-5"));
        var messages = AfterACall();
        messages.Insert(2, new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_0", "midi")]));

        var (text, calls, finish) = await ReadAsync(client, messages, WithTools());

        using var body = JsonDocument.Parse(handler.Received[0].Body);
        var sent = body.RootElement.GetProperty("messages");
        Assert.Multiple(() =>
        {
            Assert.That(body.RootElement.GetProperty("tools")[1].GetProperty("name").GetString(), Is.EqualTo("calculate"));
            Assert.That(body.RootElement.GetProperty("tools")[1].GetProperty("input_schema").GetProperty("type").GetString(), Is.EqualTo("object"));
            Assert.That(sent.GetArrayLength(), Is.EqualTo(3));
            Assert.That(sent[1].GetProperty("content")[0].GetProperty("type").GetString(), Is.EqualTo("text"));
            Assert.That(sent[1].GetProperty("content")[1].GetProperty("type").GetString(), Is.EqualTo("tool_use"));
            Assert.That(sent[1].GetProperty("content")[1].GetProperty("input").GetProperty("expression").GetString(), Is.EqualTo("12*7"));
            Assert.That(sent[2].GetProperty("role").GetString(), Is.EqualTo("user"));
            Assert.That(sent[2].GetProperty("content").GetArrayLength(), Is.EqualTo(2), "The results of a round go together.");
            Assert.That(sent[2].GetProperty("content")[1].GetProperty("type").GetString(), Is.EqualTo("tool_result"));
            Assert.That(sent[2].GetProperty("content")[1].GetProperty("tool_use_id").GetString(), Is.EqualTo("call_1"));
            Assert.That(sent[2].GetProperty("content")[1].GetProperty("content").GetString(), Is.EqualTo("84"));

            Assert.That(text, Is.EqualTo("Je calcule."));
            Assert.That(calls.Single().CallId, Is.EqualTo("toolu_1"));
            Assert.That(Argument(calls.Single(), "expression"), Is.EqualTo("12*7"));
            Assert.That(finish, Is.EqualTo(ChatFinishReason.ToolCalls));
        });
    }
}
