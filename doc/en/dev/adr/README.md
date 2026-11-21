# Architecture decision records (ADR)

One numbered ADR per decision, written and merged before the code it
describes. An accepted ADR is never edited: a new one supersedes it. The next
free number is given in the [project memory](../../memory/README.md).

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-target-dotnet-10-and-projects.md) | .NET 10 target and list of projects | Accepted |
| [0002](0002-vertical-slices-kernel-and-dependency-rules.md) | Vertical slices, shared kernel and dependency rules | Accepted |
| [0003](0003-hosts-and-native-services.md) | Hosts (Windows WPF, Web test, Android) and native services | Accepted |
| [0004](0004-configuration-storage-and-secret-vault.md) | Configuration, storage and secret vault | Accepted |
| [0005](0005-assistant-state-machine-and-orb-contract.md) | Assistant state machine and orb contract | Accepted |
| [0006](0006-voice-chain.md) | Voice chain (wake word, recognition, synthesis) | Accepted |
| [0007](0007-llm-providers-and-tool-format.md) | LLM providers and tool format | Accepted |
| [0008](0008-languages-and-localization.md) | Languages and localization | Accepted |
| [0009](0009-permission-and-confirmation-model.md) | Permission and confirmation model | Accepted |
| [0010](0010-task-fan-and-orb-click.md) | Task fan and click on the orb | Accepted |
| [0011](0011-orb-colors-and-drawing.md) | Colors and drawing of the orb | Accepted |
| [0012](0012-scripts-allowed-in-markup.md) | Script is allowed in markup (partly supersedes 0002) | Accepted |
| [0013](0013-relais-readiness.md) | Preparing the Relais module (v2): projects, subjects, tasks and orchestrator | Accepted |
| [0014](0014-license-kazo-bar-rule-and-jarvis-py.md) | Apache 2.0 license, rule on Kazo Bar (copying allowed, no reference) and removal of `jarvis.py` | Accepted |
| [0015](0015-work-tracking-source.md) | Work tracking source (GitHub Issues or Azure Boards) and mandatory link for commits and PRs | Accepted |
| [0016](0016-onnx-runtime-managed-on-sherpa-native.md) | A single native ONNX Runtime, the one from sherpa-onnx (partly supersedes 0001) | Accepted |
| [0017](0017-tools-classes-and-decision-point.md) | Basic tools, class of each tool and permission decision point (section 3 browser superseded by 0022) | Accepted |
| [0018](0018-mcp-client-and-home-assistant.md) | MCP client, Home Assistant and security home automation (clarifies 0009 for Home Assistant scripts) | Accepted |
| [0019](0019-conversation-memory.md) | Conversation memory: kept facts, summary of old exchanges, "Memory" page | Accepted |
| [0020](0020-hand-off-targets-and-spend-cap.md) | Hand-off, targets and spend cap | Accepted |
| [0021](0021-claude-code-sessions.md) | Claude Code sessions: launch, permissions, hooks, announcements | Accepted |
| [0022](0022-in-app-browser-for-web-search.md) | Web search in a browser internal to Jarvis (partly supersedes 0017) | Accepted |
| [0023](0023-confirmation-request-in-a-turn.md) | The confirmation request within a turn: "interaction required" condition and look, voice answer by a closed list, no timeout (completes 0005) | Accepted |

The first nine ADRs are those of stage 1, listed in the
[design document](../plans/design.md#working-method-and-first-adrs).
The ADRs of the later stages are written at the start of each series.

The mockups of ADR 0010 are the `0010-task-fan-*.png` images. They are only stored in the
[French folder](../../../fr/dev/adr/README.md); the English ADR links to them.
