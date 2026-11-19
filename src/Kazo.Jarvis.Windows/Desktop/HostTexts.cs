using System.Globalization;

namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// Texts of the Windows host, in French or English after the Windows display language. Provisional: the
/// translations move to resources with work item stage1/5-shared-ui-language (ADR 0008).
/// </summary>
internal static class HostTexts
{
    /// <summary>Download page of the WebView2 runtime.</summary>
    public const string WebView2Download = "https://developer.microsoft.com/microsoft-edge/webview2/";

    private static bool French => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

    public static string ApplicationName => "Kazo.Jarvis";

    public static string Settings => French ? "Paramètres" : "Settings";

    public static string Conversation => French ? "Conversation" : "Conversation";

    public static string MuteMicrophone => French ? "Couper le micro" : "Mute the microphone";

    public static string Quit => French ? "Quitter" : "Quit";

    public static string WaitingForUnlock => French
        ? "en attente du déverrouillage de la session"
        : "waiting for the session to be unlocked";

    public static string WebView2Missing => French
        ? "Kazo.Jarvis a besoin du moteur Microsoft Edge WebView2, absent de ce PC.\n\nOuvrir la page de téléchargement ?\n" + WebView2Download
        : "Kazo.Jarvis needs the Microsoft Edge WebView2 runtime, which is missing on this PC.\n\nOpen the download page?\n" + WebView2Download;

    public static string WithStatus(string status) => French ? $"{ApplicationName} : {status}" : $"{ApplicationName}: {status}";

    public static string Crashed(string logFile) => French
        ? $"Kazo.Jarvis a rencontré une erreur et doit s'arrêter.\n\nLe détail est dans le journal :\n{logFile}"
        : $"Kazo.Jarvis ran into an error and must stop.\n\nThe details are in the log:\n{logFile}";
}
