using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>What the three adapters share: sending, naming the cause of a failure, reading a stream line by line.</summary>
internal static class LlmHttp
{
    /// <summary>The longest error body kept for the log.</summary>
    private const int DetailLength = 300;

    /// <summary>JSON options of the provider APIs: property names written as given, nulls left out.</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The address with a final slash, so that relative paths add to it instead of replacing its last segment.</summary>
    /// <param name="endpoint">The configured address.</param>
    /// <returns>The base address.</returns>
    public static Uri AsBase(Uri endpoint)
    {
        var text = endpoint.GetLeftPart(UriPartial.Path);
        return new Uri(text.EndsWith('/') ? text : text + "/");
    }

    /// <summary>Whether <paramref name="endpoint"/> is an absolute <c>http</c> or <c>https</c> address.</summary>
    /// <param name="endpoint">The address.</param>
    /// <returns><see langword="true"/> when it can be called.</returns>
    public static bool IsCallable(Uri endpoint) =>
        endpoint.IsAbsoluteUri && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Sends <paramref name="request"/> and returns the response once its headers are read. A connection failure
    /// becomes <see cref="LlmFailure.Unreachable"/>, a timeout <see cref="LlmFailure.TimedOut"/>, an error status
    /// the cause it names. Cancellation by <paramref name="cancellationToken"/> is let through.
    /// </summary>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient http, HttpRequestMessage request, Func<HttpStatusCode, string, LlmFailure> classify, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new LlmException(LlmFailure.Unreachable, $"{request.RequestUri?.GetLeftPart(UriPartial.Authority)} does not answer: {exception.Message}", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmException(LlmFailure.TimedOut, $"{request.RequestUri?.GetLeftPart(UriPartial.Authority)} took too long to answer.", exception);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var body = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            throw new LlmException(classify(response.StatusCode, body), $"HTTP {(int)response.StatusCode}: {body}");
        }
    }

    /// <summary>The usual reading of an error status: 401 and 403 are a refused key, everything else a server error.</summary>
    /// <param name="status">The status.</param>
    /// <param name="body">The start of the error body.</param>
    /// <returns>The cause.</returns>
    public static LlmFailure Classify(HttpStatusCode status, string body) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LlmFailure.KeyRejected,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => LlmFailure.TimedOut,
        _ when NamesMissingModel(status, body) => LlmFailure.ModelMissing,
        _ => LlmFailure.ServerError,
    };

    /// <summary>Whether an error says the model does not exist (404, or 400 with "model ... not found").</summary>
    private static bool NamesMissingModel(HttpStatusCode status, string body) =>
        (status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        && body.Contains("model", StringComparison.OrdinalIgnoreCase)
        && (body.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || body.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || body.Contains("model_not_found", StringComparison.OrdinalIgnoreCase)
            || status == HttpStatusCode.NotFound);

    /// <summary>The start of an error body, for the log. Provider errors never echo the key.</summary>
    public static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            body = body.ReplaceLineEndings(" ").Trim();
            return body.Length <= DetailLength ? body : body[..DetailLength] + "…";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>Reads the body line by line until it ends. A cut connection becomes <see cref="LlmFailure.Unreachable"/>.</summary>
    public static async IAsyncEnumerable<string> ReadLinesAsync(HttpResponseMessage response, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stream stream;
        try
        {
            stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new LlmException(LlmFailure.Unreachable, "The answer was cut.", exception);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                throw new LlmException(LlmFailure.Unreachable, "The answer was cut.", exception);
            }

            if (line is null)
            {
                yield break;
            }

            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }

    /// <summary>Reads the JSON body of a successful response.</summary>
    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new LlmException(LlmFailure.ServerError, "The server did not answer in JSON: is it the right kind of provider?", exception);
        }
    }

    /// <summary>A JSON POST request.</summary>
    public static HttpRequestMessage Post(Uri address, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, address)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <summary>
    /// Refuses tools: the tool format of ADR 0007 (section 3) and the tolerant parser arrive at stage 2 with the
    /// first tool. Silently dropping them would make the model answer as if it had none.
    /// </summary>
    public static void RejectTools(ChatOptions? options)
    {
        if (options?.Tools is { Count: > 0 })
        {
            throw new NotSupportedException("Tools arrive at stage 2 (ADR 0007, section 3); this adapter does not send them yet.");
        }
    }

    /// <summary>
    /// The messages to send, the instructions of <paramref name="options"/> first as a system message.
    /// Only the text of each message is sent at stage 1.
    /// </summary>
    public static IReadOnlyList<(string Role, string Text)> Flatten(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var result = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
        {
            result.Add(("system", options.Instructions));
        }

        foreach (var message in messages)
        {
            var role = message.Role == ChatRole.System ? "system"
                : message.Role == ChatRole.Assistant ? "assistant"
                : message.Role == ChatRole.User ? "user"
                : throw new NotSupportedException($"Role '{message.Role}' arrives with the tools at stage 2.");
            result.Add((role, message.Text));
        }

        return result;
    }

    /// <summary>The last update of a stream: why it ended, the tokens counted, the model that answered.</summary>
    public static ChatResponseUpdate Final(string? modelId, ChatFinishReason? finish, long? input, long? output)
    {
        var contents = new List<AIContent>();
        if (input is not null || output is not null)
        {
            contents.Add(new UsageContent(new UsageDetails
            {
                InputTokenCount = input,
                OutputTokenCount = output,
                TotalTokenCount = input + output,
            }));
        }

        return new ChatResponseUpdate(ChatRole.Assistant, contents) { ModelId = modelId, FinishReason = finish };
    }
}
