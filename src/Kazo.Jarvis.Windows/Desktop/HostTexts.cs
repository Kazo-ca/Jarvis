using System.Globalization;
using System.Resources;

namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// Texts of the Windows host, from <c>HostTexts.resx</c> (English) and <c>HostTexts.fr.resx</c> (ADR 0008,
/// section 2). Static because some show before the services exist (WebView2 missing, crash): those use the
/// Windows display language; the others follow the language of the assistant once the host has started.
/// </summary>
internal static class HostTexts
{
    /// <summary>Download page of the WebView2 runtime.</summary>
    public const string WebView2Download = "https://developer.microsoft.com/microsoft-edge/webview2/";

    private static readonly ResourceManager Resources = new(typeof(HostTexts).FullName!, typeof(HostTexts).Assembly);
    private static CultureInfo culture = CultureInfo.CurrentUICulture;

    /// <summary>The language of the texts: the Windows display language, then the language of the assistant.</summary>
    public static CultureInfo Culture
    {
        get => Volatile.Read(ref culture);
        set => Volatile.Write(ref culture, value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static string ApplicationName => "Kazo.Jarvis";

    public static string Settings => Text(nameof(Settings));

    public static string Conversation => Text(nameof(Conversation));

    public static string MuteMicrophone => Text(nameof(MuteMicrophone));

    public static string Updates => Text(nameof(Updates));

    public static string UpdateFound(string version) => string.Format(CultureInfo.CurrentCulture, Text(nameof(UpdateFound)), version);

    public static string Quit => Text(nameof(Quit));

    public static string WaitingForUnlock => Text(nameof(WaitingForUnlock));

    public static string WebView2Missing => Text(nameof(WebView2Missing)) + "\n" + WebView2Download;

    public static string WithStatus(string status) => string.Format(CultureInfo.CurrentCulture, Text(nameof(WithStatus)), ApplicationName, status);

    public static string Crashed(string logFile) => string.Format(CultureInfo.CurrentCulture, Text(nameof(Crashed)), logFile);

    private static string Text(string name) => Resources.GetString(name, Culture) ?? name;
}
