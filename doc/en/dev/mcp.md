# MCP client: tool servers

Work item `stage2/6-mcp-client` (US2.2). Decision:
[ADR 0018](adr/0018-mcp-client-home-assistant-and-security-devices.md), sections 1 to 4, 6 and 7. The
classification specific to Home Assistant (section 5) is described in [home-assistant.md](home-assistant.md). The
tool registry and the decision point are described in [tools.md](tools.md).

## What is built

```
"MCP servers" page ─► McpServerHub (slice Mcp/Servers) ─► IToolServerConnector (kernel)
        │                    │  one session per enabled server            │
        │                    │  tools ─► IToolRegistry, origin mcp:<server>, off
        │                    │  headers ◄─ vault (mcp.<server>.header.<header>)
   Mcp.Servers (config.json) └─ Tools.Activation (switches)               ▼
                                            Adapters/Mcp/McpToolServerConnector (package ModelContextProtocol.Core)
                                            by address: streamable HTTP, SSE fall-back; by command: stdio
```

No server is configured at the start. A server that cannot be reached is not a fault of the assistant: its
tools leave the list given to the model, and the page says so.

## Kernel contract

`src/Kazo.Jarvis.Kernel/Tools/Servers/`, with no type of the package:

| Type | Role |
| --- | --- |
| `IToolServerConnector` | Opens a connection and does the opening exchange of the protocol |
| `IToolServerConnection` | Announced name, end of the connection (`Closed`), list of the tools, call of a tool |
| `ToolServerEndpoint` | Address, or command and arguments; headers with their values. A class, not a `record`: no generated `ToString` can print a token |
| `ToolServerTool` | Name, description, schema, "read-only" declaration |
| `ToolServerResult` | The text of the answer, and whether the tool reports a failure |
| `ToolServerFailure`, `ToolServerException` | The named cause: unreachable, token refused, timed out, command not found, protocol not understood, connection ended |

## Adapter

`McpToolServerConnector` uses the official package `ModelContextProtocol.Core` 2.4.0 (the version of spike
E14), referenced by `Kazo.Jarvis.Adapters` only. `Core` is the client and the protocol, without the hosting
extensions Jarvis does not use.

- By address: `HttpClientTransport` in automatic detection (streamable HTTP, then SSE), one `HttpClient`
  per connection. By command: `StdioClientTransport`, the process stops when the connection is closed.
- The client declares no capability: `sampling` and `elicitation` are refused by the protocol. Prompts and
  resources are not read.
- Deadline of the opening exchange: 20 s. The deadline of a call (30 s) is that of the tool loop.
- A protocol error during a call (unknown tool, wrong arguments) is an answer of the tool for the model, not
  a broken connection.

## Slice `Mcp/Servers`

### Settings

Section `Mcp.Servers`: for each server, `Name` (lower case, digits, hyphens, 30 at most), `Kind` (`Generic`
or `HomeAssistant`), `Address` or `Command` and `Arguments`, `Headers` (the **names** only), `Enabled`,
`Approved` (fingerprint of each tool turned on) and `Classes` (classes raised by hand). 20 servers at most.

The value of a header is in the vault, under `mcp.<server>.header.<header>`. The "Token" field of the add
form writes the header `Authorization` with `Bearer ` in front. Removing a server erases its secrets and
turns the switches of its tools off.

### Connection (`McpServerHub`, `McpServerSession`)

- The enabled servers connect in the background at start-up, and at each change of the settings.
- By address, a failure is tried again after 5 s, then twice as long each time, up to 5 minutes.
- A call that fails on a network error is done once more, on the same connection. A second failure is
  returned to the model; the server becomes "unreachable", its tools are removed and the attempts start
  again.
- By command, a failure to start is not tried again by itself ("Reconnect" button). A program that stops
  **keeps its tools** and is started again at the next call, three times an hour at most.
- "Test the connection" opens its own connection, counts the tools and closes it. It names the cause of a
  failure.
- When Jarvis stops, every connection is closed and the started programs are stopped.

### Tools of a server (`McpTool`)

- Name, description and schema of the server, as they are; origin `mcp:<server>`. A character a model does
  not accept in a name becomes `_`. A name already taken is prefixed: `<server>__<tool>`.
- **Off when found**, like any tool that appears later.
- **Activation tied to the fingerprint** (SHA-256 of the name, the description, the schema and the
  "read-only" declaration). When the user turns a tool on, on this page or on the "Tools" page, the
  fingerprint is kept in `Approved`. At each connection, a tool that is on and whose fingerprint changed is
  turned off **before** it enters the registry, and the page flags it until the user turns it back on. A
  switch left on without a kept fingerprint is turned off the same way.
- Class: "Read" if the server declares the tool read-only, otherwise "Sensitive" (confirmation at each
  call). The user can raise it to "Sensitive" or "Critical", never lower it: the settings refuse any other
  value. A server of type `HomeAssistant` has its own classification, per call
  ([home-assistant.md](home-assistant.md)).
- A failure reported by the tool returns to the model with its text. A connection cause returns in one
  sentence that names the server.
- `Discloses` says where the arguments go: the address, or the started program.

### "MCP servers" page

For each server: type, state with the cause, address or command, what leaves the machine, the mention
"Outside the local network" when the address is neither local nor private, the name the server gives itself,
"Connect", "Test the connection", "Reconnect", "Remove", the value of each header (secret field), then each
tool with its whole description, its class and its switch, and the estimate in tokens of the tools that are
on. At the bottom, adding by address or by local command.

Since work item 18 (ADR 0025, section 3), each generic server has an "Agent" list on the page
(`McpServerOptions.Agent`): its tools go to that agent, the Jarvis agent by default. See
[agents.md](agents.md#the-tools-of-an-agent-agentcatalogtoolsof).

## Web host

`SimulatedToolServers`: `https://mcp.jarvis.example` (`lookup_stock` read-only, `send_message`),
`https://secure.jarvis.example` (wants `Authorization: Bearer bon-jeton`), `https://down.jarvis.example`
(unreachable), command `demo-server` (`roll_dice`). The button `#revise-tool-servers` makes the description
of `send_message` change. The simulated model calls these tools on "cours de l'action …", "envoie le
message …" and "lance le dé". The `#tool-servers` element shows the calls received.

## Provisional choices of this work item

The ADR does not settle them; to be confirmed by Benoit:

1. *(Lifted by work item 7.)* The `HomeAssistant` type now has its classification:
   [home-assistant.md](home-assistant.md).
2. **A single "Token" field in the add form** (`Authorization: Bearer …`). The other headers are written in
   `config.json` (their names); their value is then typed on the page.
3. **No editing of a server** on the page: remove it and add it again.
4. **No environment variables** for a server started by command.
5. **After a change of token, "Reconnect" must be pressed**: the vault does not notify.
6. **The "read-only" declaration is part of the fingerprint**: a server that changes it gets the tool turned
   off. This is stricter than the ADR.
7. **Two servers that offer the same name**: the one that connects first keeps the plain name. If the order
   changes at another start, the tool that was on is turned off and must be turned on again.
8. **Package `ModelContextProtocol.Core`** instead of `ModelContextProtocol`: the same client, without the
   hosting extensions.

## Not verified

- **No real MCP server was reached from Jarvis.** The adapter is tested against a server of the same package
  in memory, and against hand-written HTTP responses. The server of Home Assistant was reached by spike E14,
  with the same package.
- A server started by command on Windows (`npx`, `uvx`, a `.cmd`): only the "command not found" failure is
  tested with a real process.
- The fall-back to SSE.

## Tests

- Unit: `McpServerHubTests` (nothing at the start, tools off, classes, fingerprint, prefixed name, token in
  the vault and never in `config.json`, new attempts, second failure, command started again three times an
  hour, stop), `McpToolServerConnectorTests` (list, call, tool failure, end of the server, command not
  found, 401 and 403, unreachable host, web page instead of a server).
- Playwright (`McpServersTests`): no server at the start, adding by address and by command, tools off, call
  of a tool turned on, confirmation before a tool that is not a read, raised class, tool changed by its
  server, connection test, token refused then replaced.

## Windows checks

To do with Benoit:

1. Add the server of Home Assistant by its address (`…/api/mcp`) with a token: "Connected", about 47 tools,
   all off. The token is not in `config.json`.
2. With the type "Generic", turn on `GetLiveContext` and `HassTurnOn`; "allume le salon" asks for a
   confirmation. With the type "Home Assistant", see [home-assistant.md](home-assistant.md#windows-checks). **Turn nothing on that touches a lock, the alarm, the garage door,
   the thermostat or a script.**
3. Cut the network: the server becomes "Not reachable", its tools disappear from the "Tools" page, Jarvis
   stays idle with no fault. Restore it: it comes back by itself.
4. A wrong token: "the server refused the token".
5. A server by command (for instance `npx` with a demonstration server): it starts, its tools appear; kill
   its process, then call a tool: it starts again. "Quit" stops it.
6. With `qwen3:14b`: the model picks the right tool among those that are on.
