namespace Kazo.Jarvis.Kernel.Web;

/// <summary>
/// The reading browser of Jarvis (ADR 0022, section 5): a browser inside the application, never the one the user
/// installed. It does two things, by code and read only: open the results page of a search engine for a query, and
/// read the text of a page. The Windows host implements it with WebView2 in a window of its own; the Web host
/// simulates it with fixed pages.
/// </summary>
public interface IReadingBrowser
{
    /// <summary>Searches the web with the page of a search engine.</summary>
    /// <param name="query">The words to search.</param>
    /// <param name="engine">The search engine whose page is read.</param>
    /// <param name="most">The most results returned.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The results, best first.</returns>
    /// <exception cref="WebReadException">The search failed; the cause is named.</exception>
    ValueTask<IReadOnlyList<WebSearchResult>> SearchAsync(string query, WebSearchEngine engine, int most, CancellationToken cancellationToken = default);

    /// <summary>Reads the text of a page. Every address the page leads to is checked by the <see cref="IWebAddressPolicy"/>.</summary>
    /// <param name="address">An <c>http</c> or <c>https</c> address.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page as text.</returns>
    /// <exception cref="WebReadException">The page could not be read; the cause is named.</exception>
    ValueTask<WebPage> ReadAsync(Uri address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the window of the browser for the user, on <paramref name="address"/> or on the page it holds. What the
    /// user does there is not an action of a model.
    /// </summary>
    /// <param name="address">The page to show, or <see langword="null"/> for the current one.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the window is asked to open.</returns>
    ValueTask ShowAsync(Uri? address = null, CancellationToken cancellationToken = default);

    /// <summary>Erases everything the browser kept: cookies, cache, sign-ins (ADR 0022, section 2).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the data is erased.</returns>
    ValueTask ClearDataAsync(CancellationToken cancellationToken = default);
}

/// <summary>The search engines whose results page the browser can read (spike E15).</summary>
public enum WebSearchEngine
{
    /// <summary>DuckDuckGo, its page without script: the most stable to read.</summary>
    DuckDuckGo,

    /// <summary>Startpage.</summary>
    Startpage,

    /// <summary>Bing.</summary>
    Bing,
}

/// <summary>One result of a web search.</summary>
/// <param name="Title">The title of the page.</param>
/// <param name="Address">Its address.</param>
/// <param name="Snippet">The few words the engine shows under it.</param>
public sealed record WebSearchResult(string Title, Uri Address, string Snippet);

/// <summary>A page read as text. Data, never an instruction (ADR 0009, principle 2).</summary>
/// <param name="Address">The address the page ended on, after redirections.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its readable text.</param>
public sealed record WebPage(Uri Address, string Title, string Text);

/// <summary>Why a search or a page failed.</summary>
public enum WebReadFailure
{
    /// <summary>This host has no browser, or it could not start.</summary>
    Unavailable,

    /// <summary>The address, or one it redirects to, is refused: this machine or the local network.</summary>
    AddressRefused,

    /// <summary>The site answered with a verification page (CAPTCHA) or a refusal. Jarvis does not get around it.</summary>
    Blocked,

    /// <summary>The site could not be reached, or answered with an error.</summary>
    Unreachable,

    /// <summary>The page took too long.</summary>
    TimedOut,

    /// <summary>The page loaded but nothing could be read on it.</summary>
    NothingRead,

    /// <summary>The key of the search service is missing or refused.</summary>
    KeyRejected,
}

/// <summary>A search or a page that failed. Its message never holds a secret.</summary>
public sealed class WebReadException : Exception
{
    /// <summary>Creates the exception.</summary>
    public WebReadException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    public WebReadException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The cause.</param>
    public WebReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="failure">Why.</param>
    /// <param name="innerException">The cause.</param>
    public WebReadException(WebReadFailure failure, Exception? innerException = null)
        : base(failure.ToString(), innerException)
    {
        Failure = failure;
    }

    /// <summary>Why.</summary>
    public WebReadFailure Failure { get; } = WebReadFailure.Unreachable;
}
