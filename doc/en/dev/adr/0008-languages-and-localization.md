# ADR 0008: languages and localization

## Status

Accepted by Benoit on 2026-11-19 (proposed on 2026-11-18, work item `spikes/11-stage-1-adrs`).

- **Sources:** decisions 19, 26 and 32 of the [design](../plans/design.md); stories US1.4, US1.11 and
  US1.15; assumption 9; Kazo Os ADR 0104 (localization).
- **Completes:** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (each slice carries its
  texts), [ADR 0006](0006-voice-chain.md) and [ADR 0007](0007-llm-providers-and-tool-format.md).

## Context

- French and English are mandatory from the first version. Other languages wait for v2.
- The language is chosen in the first-run wizard and changed later in the settings. The choice applies to
  the interface, to recognition and to the voice (US1.4).
- The default persona is the butler, in French and in English (US1.15).
- Kazo Os uses `.resx` resources with `IStringLocalizer`, English as the neutral language, and a test that
  compares the keys of both languages.

## Decision

### 1. A single language setting

- Jarvis has **one** setting, the assistant's language: `fr` or `en`. It sets all at once:

| What follows the language | How |
| --- | --- |
| Interface texts | Resources of the language |
| Recognition | Whisper receives the language, without automatic detection |
| Voice | The default voice of that language (US1.11) |
| Persona | The system message of that language |
| Rule given to the model | "Answer in English" or its French equivalent |

- On first run, the proposed language is that of Windows: French if the Windows interface is in French,
  English otherwise.
- Formats (dates, times, numbers) follow the Windows regional settings, not this setting. A user in French
  in Quebec keeps their formats.
- **A language change applies without restarting the application.** Open windows reload their content, and
  recognition and voice change at the next turn.

### 2. Resources

- `.resx` files and `IStringLocalizer`. **English is the neutral language** (`Name.resx`), French is in
  `Name.fr.resx`.
- **Each slice carries its resources**, in its folder. Texts common to three slices or more go to
  `Features/Shared/`.
- No visible text is hard-coded in a component or in an error message shown to the user.
- Key missing in French: the English text is shown, with a warning in the log in debug mode.
- Hosts only publish these two languages (`SatelliteResourceLanguages` set to `en;fr`).

### 3. Voice and recognition

- Each language has its default voice, chosen in the voice test page (US1.11). Starting values:
  `fr_FR-tom-medium` and `en_GB-alan-medium` (ADR 0006).
- Whisper works in one language at a time. A sentence said in the other language will be transcribed
  poorly: this is the price of faster and more reliable recognition.
- The wake word is "hey jarvis" in both languages (assumption 9).

### 4. Persona

- Two default system messages, one per language, provided as resources: the butler from the film, polite,
  concise, dry humor, formal address and "Monsieur" in French, its equivalent in English.
- A version edited by the user is saved per language in the configuration. "Reset" goes back to the default
  text of the language.

### 5. What is not translated

- Code, file names, identifiers, tool names (ADR 0007) and technical logs stay in English.
- Configuration keys and collection names are not translated.
- The documentation follows its own rule: French first, English mirrored (`AGENTS.md`).

### 6. Tests

- A test checks that each `.fr.resx` has all the keys of its `.resx`, and the other way round.
- A Playwright test changes the language in the Web host and checks a settings page and the conversation.

### 7. Version 2

- Adding a language means adding resources, a default voice, the Whisper language and a system message. No
  slice should have to change code.

## Consequences

- Every slice writes its texts in both languages from its first version. The key test is part of the merge
  bar.
- A household where both languages are spoken has to choose one at a time. Switching from one to the other
  by voice is not planned in the first version.
- Mobile (stage 4) reuses the same resources, since it shows the same slices.
- Live reloading of the windows has to be proven in the first slice that has a settings page. If it is not
  reliable in `BlazorWebView`, a new ADR will say that the interface language change applies at the next
  start.
