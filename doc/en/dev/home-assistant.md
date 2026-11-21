# Home Assistant: classification of calls and security home automation

Work item `stage2/7-home-assistant` (US2.3). Decision:
[ADR 0018](adr/0018-mcp-client-home-assistant-and-security-devices.md), section 5. The MCP client, the
connection and the servers page are described in [mcp.md](mcp.md); the decision point in
[tools.md](tools.md) and the confirmation request in [confirmation.md](confirmation.md).

## The problem

The name of a Home Assistant tool does not say what it does: `HassTurnOff` turns a light off **and unlocks a
lock**, and the server announces everything that is not a read as destructive (spike E14). So Jarvis classes
each call by code, **from the devices it aims at**.

## What is built

```
tool call ─► decision point ─► ITool.ClassifyAsync ─► McpTool (slice Mcp/Servers)
                                                         │ server of type HomeAssistant
                                                         ▼
                          IToolServerClassifier (kernel) ◄─ HomeAssistantClassifier (slice Mcp/HomeAssistant)
                                                         │ reads GetLiveContext through the connection of the server
                                                         ▼
                              Read · Everyday effect · Sensitive · Critical (click required)
```

A server is declared of type "Home Assistant" when it is added, on the "MCP servers" page. The two slices do
not know each other: `Mcp/Servers` asks the kernel for an `IToolServerClassifier` of the type
`home-assistant`.

## Classification of a call (`HomeAssistantClassifier`)

The tools are named `<platform>__<intent>` (`intent__HassTurnOn`); the intent decides. In this order:

| Tool | Class |
| --- | --- |
| Read: `GetLiveContext`, `GetDateTime`, `get_items`, or announced read-only | Read |
| Script: `script__…`, `intent_script__…` | Everyday effect; **Critical** if the user marked it "security" |
| Tool that aims at entities: its schema has `name`, `area`, `floor`, `domain` or `device_class` | **Critical** if a designated entity is a security one, Everyday effect otherwise |
| Other known tool: `HassBroadcast`, `HassCancelAllTimers`, `HassListAddItem`, `HassListCompleteItem`, `HassListRemoveItem` | Everyday effect |
| Any other tool | Sensitive (a confirmation at each call) |

**An entity is a security one** if its domain is `lock`, `alarm_control_panel`, `valve` or `siren`, if it is
a `cover` of class `garage`, `door` or `gate`, or if the user put it in the "security" list.

### Designated entities

For a tool that aims at entities, Jarvis itself calls `GetLiveContext` on the connection of the server,
whether the tool is turned on for the model or not, and reads the list of the exposed entities
(`LiveContext.Parse`: names and aliases, domain, rooms, device class).

- The `domain` and `device_class` filters of the call first set aside the entities they exclude.
- A `name` that is the exact name or alias of an entity (whatever the case) and an `area` that is one of its
  rooms designate these entities.
- **If nothing matches** (approximate name, alias missing from the list, name and room that do not agree), if
  the call gives a floor (`floor`, which the list does not show) or if it gives neither a name nor a room:
  every entity the filters do not exclude counts. With a lock exposed and a call without a filter, it is
  "Critical".

### Refuse rather than guess

- If `GetLiveContext` does not answer, or if no entity can be read from its answer (changed format, nothing
  exposed), the classification fails: the decision point denies the call (`ClassifierFailed`) and writes it
  to the permission log. Jarvis never classes "Everyday effect" for lack of information.
- The list read is kept 5 seconds per server (can be set from 0 to 60; 0 reads it again at each call), so
  that it is not asked again at each call of the same turn.
- A class the user raised on the "MCP servers" page stays a floor: the classification never goes below it.
- "Critical" asks for the click on the PC: the voice is not enough (ADR 0009, ADR 0023).

## "Security" list and "Home Assistant" page

Settings section `Mcp.HomeAssistant`: `SecurityEntities` (entity names), `SecurityScripts` (names of script
tools), `ContextSeconds`. 200 entries at most per list.

The "Home Assistant" page of the settings recalls what is always a security device, offers a field of one
entity name per line, the list of the scripts of the connected servers with a "security" box each, and how
long the list is kept. On the "MCP servers" page, each script of a Home Assistant server carries the
reminder that a script can actuate anything.

## Kernel contracts added

| Type | Role |
| --- | --- |
| `ITool.ClassifyAsync` | The class of a call when a server must be asked to know it; by default, `Classify`. The decision point only calls this one |
| `IToolServerClassifier` (`Kernel/Tools/Servers`) | The classification of one type of server: lowest class of a tool, class of a call |
| `ToolServerCall` | The server, the tool, the arguments and a way to ask the server a question |
| `ToolServerKinds` | The name of the type `home-assistant` |

## Web host

`SimulatedToolServers`: `https://maison.jarvis.example`, a Home Assistant with
`homeassistant__GetLiveContext`, `intent__HassTurnOn`, `intent__HassTurnOff`, `script__soiree_cinema` and
`script__depart`. It exposes "Lampe du salon", "Serrure de l'entrée", "Porte du garage", "Store du bureau"
and "Prise du sapin". The simulated model calls these tools on "allume …", "éteins …", "déverrouille …"
("tout dans la pièce …" aims at a room) and "lance le script …".

## Provisional choices of this work item

The ADR does not settle them; to be confirmed by Benoit:

1. **The settings section is named `Mcp.HomeAssistant`**, not `Mcp.HomeAssistant.Security` as the ADR writes:
   a settings section has a single dot. The list has **its own page**, "Home Assistant", not the page of the
   server.
2. **An entity is marked by its name**, typed by hand: MCP does not give the identifier of the entity. An
   entity renamed in Home Assistant must be marked again.
3. **The "security" list applies to every Home Assistant server** configured.
4. **A call that gives a floor** is classed as if its target was not found: `GetLiveContext` does not show
   the floors.
5. **The list of the entities is kept 5 seconds.** A lock exposed in Home Assistant during these 5 seconds
   is not seen yet.
6. **The thermostat is not a security device** by nature (the ADR does not name it): it is marked by hand.

## Not verified

- **No call left Jarvis for the real Home Assistant.** The classification is tested against an answer of
  `GetLiveContext` written by hand from spike E14; the real format may differ (in that case, the calls are
  denied, not allowed).
- Nothing has run on Windows.

## Tests

- Unit: `HomeAssistantClassifierTests` (reading the answer, lowest class, light and lock with the same tool,
  room, target not found, marked entity, marked script, known tools, denial without the list, older name of
  `GetLiveContext`, keeping the list, settings), `McpServerHubTests` (a server of a type classed by its
  classification), `PermissionDecisionPointTests` (class found after a wait).
- Playwright (`HomeAssistantTests`): light turned on without a question, lock and room of the lock with a
  click required and nothing run before, script without a question then "Critical" once marked, marked
  entity.

## Windows checks

To do with Benoit. **Never approve an action on a lock, the alarm, the garage door, the thermostat or a
script: the request for a click is checked, then refused.**

1. Add the server with the type "Home Assistant": `HassTurnOn` and `HassTurnOff` are of class "Everyday
   effect", the scripts carry the reminder.
2. Turn on `HassTurnOn` and `HassTurnOff`; "allume le salon" and "turn on the living room" turn on without a
   question (US2.3).
3. "Déverrouille la porte d'entrée": "Critical" window, the voice is not enough; **refuse**. The permission
   log shows the denial.
4. An approximate name ("éteins le truc"): "Critical" since a lock is exposed; refuse.
5. "Home Assistant" page: the 21 scripts or so are listed; mark one, without running it.
6. Cut the network then ask to turn on a light: the call is denied, not run.
7. Compare a real answer of `GetLiveContext` with the expected format (names, `domain`, `areas`,
   `device_class`).
