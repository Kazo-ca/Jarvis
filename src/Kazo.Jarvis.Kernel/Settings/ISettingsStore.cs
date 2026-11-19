namespace Kazo.Jarvis.Kernel.Settings;

/// <summary>
/// The user's settings (ADR 0004, section 2): one section per slice in a single file, <c>config.json</c>,
/// which never holds a secret. The Windows host uses the file, the Web host an in-memory store.
/// </summary>
public interface ISettingsStore
{
    /// <summary>What happened when the settings were loaded. Says whether they can be saved.</summary>
    SettingsLoadState State { get; }

    /// <summary>
    /// The saved value of <paramref name="section"/>, or its default values when the section is absent,
    /// cannot be read or is not valid. Each call returns a new instance.
    /// </summary>
    /// <typeparam name="T">The options class.</typeparam>
    /// <param name="section">The section declared by the slice.</param>
    /// <returns>The value to use.</returns>
    T Read<T>(SettingsSection<T> section)
        where T : class;

    /// <summary>
    /// Validates then saves <paramref name="value"/>, and notifies the watchers of the section. Nothing is
    /// written when the value is not valid, or when the settings cannot be saved (<see cref="SettingsLoadState.CanSave"/>).
    /// </summary>
    /// <typeparam name="T">The options class.</typeparam>
    /// <param name="section">The section declared by the slice.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">Cancels the save before it starts writing.</param>
    /// <returns>Whether the value was saved, and why not.</returns>
    ValueTask<SettingsWriteResult> WriteAsync<T>(SettingsSection<T> section, T value, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Calls <paramref name="handler"/> with the new value after each save of <paramref name="section"/>,
    /// so a changed setting applies without a restart.
    /// </summary>
    /// <typeparam name="T">The options class.</typeparam>
    /// <param name="section">The section declared by the slice.</param>
    /// <param name="handler">Receives a new instance of the saved value. Its exceptions are logged and do not cancel the save.</param>
    /// <returns>Disposing it ends the subscription.</returns>
    IDisposable Watch<T>(SettingsSection<T> section, Action<T> handler)
        where T : class;
}
