# Design prototypes

*Version française : voir [doc/fr/dev/ui/catalog.md](../doc/fr/dev/ui/catalog.md).*

Visual references validated by Benoit on 19 November 2026. They are self-contained HTML pages: open one in a
browser to see it animate. They are **not product code**: the application ports them to Razor components.
Their on-screen labels are in French. The written description is the UI catalog
([en](../doc/en/dev/ui/catalog.md), [fr](../doc/fr/dev/ui/catalog.md)).

| File | What it shows |
| --- | --- |
| [prototypes/orb-high-quality.html](prototypes/orb-high-quality.html) | High-quality orb: plasma sphere, binary planet clouds, gyroscope rings, the 7 states |
| [prototypes/orb-light.html](prototypes/orb-light.html) | Light orb: still gradient sphere, three open rings in opposite directions, random pulses while speaking |
| [prototypes/orb-plasma-gyroscope.html](prototypes/orb-plasma-gyroscope.html) | Plasma and rings without clouds, kept aside |
| [prototypes/task-fan.html](prototypes/task-fan.html) | Task fan around the orb (`?v=A&s=open&c=br`, corners `tl` `tr` `bl` `br`) |
| [prototypes/full-screen.html](prototypes/full-screen.html) | Full-screen "command post" (1600 × 900) |
| [images/](images/) | Still captures of the three main designs |

The high-quality prototype also contains earlier binary-layer modes (1 to 4); mode 5 (`window.BIN=5`) is the
validated one.
