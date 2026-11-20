using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Localization;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Settings.Language;

/// <summary>
/// Keeps the language of the assistant (ADR 0008, section 1): the saved choice, or the language proposed from
/// Windows while there is none. Watches the setting, so a change applies without a restart, and announces it
/// with <see cref="AssistantLanguageChanged"/>.
/// </summary>
public sealed partial class LanguageKeeper : IAssistantLanguageReader, IDisposable
{
    private readonly ISettingsStore settings;
    private readonly IMessageBus bus;
    private readonly ILogger<LanguageKeeper> logger;
    private readonly IDisposable watch;
    private AssistantLanguage current;

    /// <summary>Creates the keeper and reads the setting.</summary>
    /// <param name="settings">The settings, for <see cref="LanguageOptions.Section"/>.</param>
    /// <param name="system">The Windows display language, for the proposal.</param>
    /// <param name="bus">Where the change is announced.</param>
    /// <param name="logger">Logs the changes.</param>
    public LanguageKeeper(ISettingsStore settings, ISystemLanguage system, IMessageBus bus, ILogger<LanguageKeeper> logger)
    {
        ArgumentNullException.ThrowIfNull(system);
        this.settings = settings;
        this.bus = bus;
        this.logger = logger;
        Proposed = AssistantLanguage.ProposedFor(system.DisplayCulture);

        // Watch before reading: a save between the two is not missed.
        watch = settings.Watch(LanguageOptions.Section, Apply);
        current = Resolve(settings.Read(LanguageOptions.Section));
    }

    /// <inheritdoc />
    public AssistantLanguage Current => Volatile.Read(ref current);

    /// <summary>The language proposed from the Windows display language, used until the user chooses.</summary>
    public AssistantLanguage Proposed { get; }

    /// <summary>Whether the language was chosen by the user rather than proposed from Windows.</summary>
    public bool IsChosen => AssistantLanguage.TryParse(settings.Read(LanguageOptions.Section).Code, out _);

    /// <summary>Saves the choice of the user; the change applies at once (ADR 0008, section 1).</summary>
    /// <param name="language">The chosen language.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Whether the choice was saved, and why not.</returns>
    public ValueTask<SettingsWriteResult> ChooseAsync(AssistantLanguage language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(language);
        return settings.WriteAsync(LanguageOptions.Section, new LanguageOptions { Code = language.Code }, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => watch.Dispose();

    private AssistantLanguage Resolve(LanguageOptions options) =>
        AssistantLanguage.TryParse(options.Code, out var chosen) ? chosen : Proposed;

    private void Apply(LanguageOptions options)
    {
        var next = Resolve(options);
        var previous = Interlocked.Exchange(ref current, next);
        if (previous.Equals(next))
        {
            return;
        }

        LogChanged(previous.Code, next.Code);

        // The settings store calls back inside the save: the delivery to the windows must not hold it.
        _ = Task.Run(async () =>
        {
            try
            {
                await bus.PublishAsync(new AssistantLanguageChanged(previous, next)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogPublishFailed(exception);
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Assistant language changed: {Previous} -> {Current}.")]
    private partial void LogChanged(string previous, string current);

    [LoggerMessage(Level = LogLevel.Error, Message = "The language change could not be published.")]
    private partial void LogPublishFailed(Exception exception);
}
