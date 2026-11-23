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
| [prototypes/projects-rail.html](prototypes/projects-rail.html) | Full screen with the project rail (ADR 0026, proposed) |
| [prototypes/projects-all.html](prototypes/projects-all.html) | "All projects" cards (ADR 0026, proposed) |
| [prototypes/projects-subject-thread.html](prototypes/projects-subject-thread.html) | Subject thread, task strip, "T3>" answers, task thread (ADR 0026, proposed) |
| [prototypes/projects-general-to-project.html](prototypes/projects-general-to-project.html) | Jarvis offers to make a project of a Général subject (ADR 0026, proposed) |
| [images/](images/) | Still captures of the three main designs |

The high-quality prototype also contains earlier binary-layer modes (1 to 4); mode 5 (`window.BIN=5`) is the
validated one.
