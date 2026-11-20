# ADR 0018: MCP client, Home Assistant and security home automation

## Status

Proposed on 2026-11-20 (work item `stage2/2-adrs`). The decisions Benoit took after spike E14 are in the
"Benoit's answers" section.

- **Settles the points left by:** [ADR 0009](0009-permission-and-confirmation-model.md) (section 2: "how to
  recognize security home automation"; section 8: Home Assistant commands go through MCP).
- **Clarifies:** ADR 0009, section 2, "a write, a command or a script is never classed below
  'Sensitive'". That "script" is a script run on the PC (ADR 0009, section 6). A Home Assistant script is a
  home automation action, classed by section 5 of this ADR.
- **Stories:** US2.2 and US2.3 of the [design document](../plans/design.md).
- **Spike:** E14 ([results](../../../../spikes/e14-mcp-home-assistant/RESULTS.md), in French).
- **Builds on:** [ADR 0017](0017-tools-classes-and-decision-point.md) (tool registry, per-call class,
  decision point).

## Context

- Spike E14: the official `ModelContextProtocol` 2.4.0 package talks to Home Assistant's MCP server over
  streamable HTTP (`/api/mcp`). 47 tools, all usable as is as `AIFunction`. The server is stateless: after an
  outage, the same client resumes without redoing anything.
- The `HassTurnOn` and `HassTurnOff` tools lock and **unlock** locks. MCP annotations mark as "destructive"
  everything that is not a read, a player's pause like a lock. Each exposed script is a tool without
  parameters, whose name does not say what it does.
- MCP gives neither Home Assistant's labels nor its zones. `GetLiveContext` gives the domain, the area and
  the device class of each exposed entity.
- At Benoit's, no lock, alarm or garage door is exposed to Assist today.
- An MCP tool's description is written by its server: it is text that enters the model's context.

## Decision

### 1. Client and transports

- `ModelContextProtocol` package (2.4.0 in the spike), referenced by `Kazo.Jarvis.Adapters` only. The kernel
  carries a "tool server" contract with no type from the package.
- Two ways to add a server (US2.2):
  - **by address**: streamable HTTP, falling back to SSE (the package's automatic detection);
  - **by local command**: `stdio` transport; the process is started by Jarvis and closed on "Quit".
- Settings section `Mcp.Servers`: name, type (`generic` or `home-assistant`), address or command and
  arguments, headers, enabled or not. **Tokens and header values go to the vault**
  ([ADR 0004](0004-configuration-storage-and-secret-vault.md)); the setting only keeps the reference.
- **The `home-assistant` type is chosen by the user** when adding. It is never deduced from the name the
  server announces: a server cannot give itself lower classes.

### 2. Connection

- "Test the connection" names the cause of a failure: server unreachable, token refused, timeout, command
  not found, protocol not understood.
- Enabled servers connect in the background at start-up. An unreachable server **is not a fault of the
  assistant**: its tools are removed from the list given to the model, the page says so, and the connection
  is retried (5 s, then doubling up to 5 minutes).
- Timeout of a call: 30 s. On a network error, one retry, without rebuilding a session (E14). A second
  failure is returned to the model as the tool's result.
- A server started by command that stops is restarted on the next call, at most three times per hour.

### 3. Tools of a server

- The tools enter the ADR 0017 registry with the origin `mcp:<server>`, **with the server's name, description
  and schema, as is**. Two servers giving the same name: the second is prefixed with its server's name.
- **Every MCP tool is disabled when discovered** (US2.2: activation tool by tool). A tool that appears later
  on an already configured server is disabled too.
- **Activation is tied to the fingerprint** of the name, the description and the schema. If one changes, the
  tool goes back to "disabled" until the user reviews it: a server cannot silently change what an approved
  tool says it does.
- A server's page shows each tool with its whole description, its class and its switch, and the estimate of
  the size of the enabled list (ADR 0017, section 4).
- A server's prompts and resources are not used at this stage. A server's requests to the client
  (`sampling`, `elicitation`) are refused.

### 4. Class of an MCP tool of type `generic`

| What the server says | Class |
| --- | --- |
| `readOnly` annotation | Read |
| Everything else | Sensitive: confirmation on every call |

- The user can raise a tool's class ("Sensitive", "Critical"), never lower it: a server's annotation is a
  statement, not a proof.
- The `readOnly` annotation is believed for the class, but the result remains data.

### 5. Class of a tool of type `home-assistant`

Jarvis code classifies each call, **after finding the targeted entities** (answer 1).

1. **Reads** (`GetLiveContext`, `GetDateTime`, `todo__get_items`, and any tool annotated `readOnly`): "Read".
2. **Tool that targets entities** (it has the `name`, `area`, `floor`, `domain` or `device_class`
   parameters: `HassTurnOn`, `HassTurnOff`, `HassLightSet`, the player and vacuum commands, and any future
   tool of the same shape):
   - Jarvis reads `GetLiveContext` and looks for the entities the call designates;
   - if one of them is a **security** entity, the call is "Critical"; otherwise "Everyday effect";
   - **if the target is not found** (an alias `GetLiveContext` does not show, an approximate name), the call
     takes the highest class among **all** exposed entities that its `domain` and `device_class` filters do
     not exclude. With no lock or alarm exposed, that is "Everyday effect"; with an exposed lock and a call
     without a filter, it is "Critical".
3. **Scripts** (`script__…`, `intent_script__…`): "Everyday effect" by default (answer 2). A script the user
   marked as "security" is "Critical".
4. **Any other tool** with no recognizable target: "Everyday effect" if it is in the known list
   (`HassBroadcast`, `HassCancelAllTimers`, the three `todo` list tools), otherwise "Sensitive".

**An entity is a security entity** if:

- its domain is `lock`, `alarm_control_panel`, `valve` or `siren`;
- or its domain is `cover` and its device class is `garage`, `door` or `gate`;
- or the user put it in the "security" list.

The "security" list (entities and scripts) is the settings section `Mcp.HomeAssistant.Security`, on the
server's page. When a script is enabled, the page reminds that a script can operate anything, and offers to
mark it.

A "Critical" action asks for the click on the PC: voice is not enough (ADR 0009, section 2).

### 6. What leaves the machine

- An MCP server by address is an outbound connection the user set up themselves. The add screen says the
  arguments of each call go to that address. A server outside the local network shows it plainly.
- No server is configured by default.

### 7. Slices

- `Mcp/Servers`: the servers, their page, the connection, tool activation.
- `Mcp/HomeAssistant`: the classification of section 5 and the "security" list.
- `Adapters`: the package's client, behind the kernel's "tool server" contract.
- The two slices do not reference each other: `Mcp/HomeAssistant` registers its classifier in the registry
  for servers of type `home-assistant`, through a kernel contract.

## Benoit's answers

Questions asked in the conversation on 2026-11-20, after spike E14. Answer: "I agree with your
recommendations".

1. **How does Jarvis recognize security home automation?** From the targeted device (lock, alarm, valve,
   siren, garage door or gate require the click), plus a list the user completes.
2. **Do Home Assistant scripts ask for a confirmation?** Not by default. The user marks as "security" those
   that must require the click.

## Options set aside

- **Classifying from the tool name or its annotations.** `HassTurnOff` unlocks a lock, and everything is
  annotated "destructive" (E14).
- **Reading Home Assistant's labels through the WebSocket API.** Possible (spike E9), but a second source to
  keep in agreement with the first; Jarvis's "security" list is enough at this stage.
- **Scripts "Sensitive" by default.** Set aside by Benoit: confirming "Netflix evening" every time would be
  tiresome.
- **Enabling a newly added server's tools automatically.** Against denial by default, and 47 tools at once
  in a 4096-token context.

## Consequences

- **Accepted risk:** a script not marked as "security" goes through without a question, whatever it does. The
  page says so when each script is enabled. The best protection remains not exposing locks and the alarm to
  Assist in Home Assistant.
- A generic MCP tool that is not a read asks for a confirmation on every call. This is intended for the first
  version; a lasting agreement per tool would need a new ADR (ADR 0009, consequences).
- Each call that targets entities costs one read of `GetLiveContext` (0.3 s in the spike). Jarvis may keep it
  in memory for a few seconds; the duration is set in work item `stage2/7-home-assistant`.
- **Not verified:** the classification of a real lock (none is exposed at Benoit's); the local model picking
  the right tool among many Home Assistant tools; the room the enabled list takes in 4096 tokens. These
  points are measured in work item 7, with a test server for locks.
