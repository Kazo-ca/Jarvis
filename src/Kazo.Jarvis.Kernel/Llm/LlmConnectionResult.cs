namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>Result of « Tester la connexion » (ADR 0007, section 1).</summary>
/// <param name="Failure">The cause when the test failed; <see langword="null"/> when it passed.</param>
/// <param name="Detail">Technical detail for the log, in English. Never holds a key.</param>
/// <param name="Elapsed">How long the test took.</param>
/// <param name="AvailableModels">The models the server lists, when it lists them; helps pick the right name.</param>
public sealed record LlmConnectionResult(
    LlmFailure? Failure,
    string? Detail,
    TimeSpan Elapsed,
    IReadOnlyList<string> AvailableModels)
{
    /// <summary>Whether the server answered and has the model.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>A passed test.</summary>
    /// <param name="elapsed">How long it took.</param>
    /// <param name="models">The models the server lists.</param>
    /// <returns>The result.</returns>
    public static LlmConnectionResult Passed(TimeSpan elapsed, IReadOnlyList<string> models) => new(null, null, elapsed, models);

    /// <summary>A failed test.</summary>
    /// <param name="failure">The cause.</param>
    /// <param name="detail">Technical detail for the log.</param>
    /// <param name="elapsed">How long it took.</param>
    /// <param name="models">The models the server lists, if it answered.</param>
    /// <returns>The result.</returns>
    public static LlmConnectionResult Failed(LlmFailure failure, string? detail, TimeSpan elapsed, IReadOnlyList<string>? models = null) =>
        new(failure, detail, elapsed, models ?? []);
}
