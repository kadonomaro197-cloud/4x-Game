# Design Tools Index — the 17 interactive prototypes

**What this is:** the one place that lists every **clickable HTML design/sim tool** in the repo, grouped by purpose. These are self-contained prototypes you open in a browser — the *blueprint* of the in-game tools, each honestly grading which parts the engine actually does today. **None is wired into the game engine.**

> For the deeper written design behind each tool, follow its companion doc (right-hand column) or `docs/DOCS-INDEX.md`. For what the engine still has to build behind them, see `docs/BUILD-BACKLOG-INDEX.md`.

**As of 2026-08-13.**

## The Component Designer — 12 "door" designers
Folder: `docs/Actual HTMLs Of designers/` (see its `README.md`). Each models one **component**. Method: `docs/economy/DESIGNER-NORTH-STAR.md`.

| Tool | Door |
|------|------|
| `Actual HTMLs Of designers/weaponsderived.html` | Weapons |
| `Actual HTMLs Of designers/defensederived.html` | Shields + armour |
| `Actual HTMLs Of designers/powerderived.html` | Reactors / power |
| `Actual HTMLs Of designers/propulsionderived.html` | Engines (thrust / warp / ground) |
| `Actual HTMLs Of designers/sensorsderived.html` | Sensors / detection |
| `Actual HTMLs Of designers/chassisderived.html` | Hull / chassis |
| `Actual HTMLs Of designers/commandderived.html` | Command / seats |
| `Actual HTMLs Of designers/enhancersderived.html` | Enhancers |
| `Actual HTMLs Of designers/auraderived.html` | Aura / field emitters |
| `Actual HTMLs Of designers/logisticalderived.html` | Cargo / fuel / logistics |
| `Actual HTMLs Of designers/industrialderived.html` | Production / mining |
| `Actual HTMLs Of designers/civicderived.html` | Civic infrastructure |

## The assembler + build dossiers
| Tool | Purpose | Companion doc |
|------|---------|---------------|
| `assembler/entityassembler.html` | Mount designed components onto a hull → a whole ship / ground unit / station, with live budgets, gates, and emergent totals | `assembler/DESIGNER-DRIVER-PLAYBOOK.md`, `assembler/02-IO-MATRIX.md` |
| `assembler/franchise-cards.html` | Build-cards for the franchise units (Venator, Acclamator, Sovereign, Death Star, …) | `assembler/FRANCHISE-UNITS-BUILD.md` + the per-ship `*-BUILD.md` |

## The simulators + views
| Tool | Purpose | Companion doc |
|------|---------|---------------|
| `combat/resolversim.html` | Watch the real auto-resolver math run one salvo at a time — space / ground / air, with a 30-environment selector | `combat/RESOLVER-SIM.md`, `combat/ENVIRONMENT-CONDITIONS-DESIGN.md` |
| `combat/forceswindow.html` | The unified **Force Management / order-of-battle** window — every owned unit in one roster, the 123-order menu, the component-scan | `combat/FORCES-WINDOW-DESIGN.md` |
| `ground/planetview.html` | The planet surface as the game models it — real Sol worlds, terrain, resources, a self-sufficient civilization, weather, units on the map | `ground/PLANETARY-VIEW-AND-INTERACTION-DESIGN.md`, `ground/UNITS-ON-THE-MAP-DESIGN.md` |

## How they fit together
The **12 door designers** produce components → the **Entity Assembler** sums them into the totals a unit fights with → the **resolver sim** shows those totals fighting → the **Forces window** commands the units you've built → the **planet view** is the ground they fight over. The whole chain is graded LIVE / DATA / BUILD at every step; the unbuilt half is catalogued in `docs/BUILD-BACKLOG-INDEX.md`.
