# The Component Designer — the 12 "door" HTMLs

**What this folder is:** the **canonical Component Designer**, as of the 2026-08-03 designer-docs cleanup. Each `*derived.html` file is **one "door"** — one category of part you can design. A door models a single **component**; the [Entity Assembler](../assembler/entityassembler.html) is what bolts your designed components onto a hull to make a whole ship, ground unit, or station.

These are **self-contained, clickable design prototypes** (open in a browser). They are the *blueprint* of what the in-game designer is meant to be — every one honestly flags, in red, any dial whose engine wiring isn't built yet. Nothing here is wired into the game engine; they are the intention documents the engine is catching up to.

## The 12 doors

| File | The door it designs |
|------|---------------------|
| `weaponsderived.html` | **Weapons** — nature × delivery; the weapon-triangle position emerges |
| `defensederived.html` | **Defense** — shields + armour |
| `powerderived.html` | **Power** — reactors / energy generation |
| `propulsionderived.html` | **Propulsion** — thrust / warp / ground locomotion |
| `sensorsderived.html` | **Sensors** — detection / EMCON |
| `chassisderived.html` | **Chassis** — the hull (mass + structure budget everything else spends) |
| `commandderived.html` | **Command** — bridge / admin seats / span-of-control |
| `enhancersderived.html` | **Enhancers** — capability boosts (e.g. the caliber-firepower multiplier) |
| `auraderived.html` | **Aura** — field-effect emitters (buff/debuff auras — mostly a proposal; Command + Ward shipped 2026-08 as fleet/battalion scalar folds, Rally/Dread/Jamming still unbuilt) |
| `logisticalderived.html` | **Logistical** — cargo / fuel / transfer / logistics modules |
| `industrialderived.html` | **Industrial** — production / mining facilities (crew coefficients live here) |
| `civicderived.html` | **Civic** — colony infrastructure (jobs / amenity / public-order / commerce dials) |

## The three companions to read alongside these

1. **The method — `../economy/DESIGNER-NORTH-STAR.md`** 🔒. *How* each of these pages is derived: a door is not invented, it is derived from the values the simulation actually reads. The two tests every dial must pass (does it write a sim variable? can it be set without knowing anything else on the entity?) live here.
2. **The totals machine — `../assembler/`**. The doors model components; the resolvers read *totals*; the Entity Assembler (`entityassembler.html`) turns designed components into those totals. The per-door input/output census is `../assembler/01-IO-<door>.md`; the master wiring matrix is `../assembler/02-IO-MATRIX.md`.
3. **The engine gaps behind the intentions — `../assembler/ENGINE-WIRING-BACKLOG-2026-08-06.md`**. The punch-list of dials the tool draws but the engine hasn't welded yet. When a dial's pipe gets welded, flip its honesty flag here *and* its row there.

## For the whole toolset at a glance
See **`../DESIGN-TOOLS-INDEX.md`** — the landing page listing all 17 interactive design/sim tools (these 12 + the assembler, the franchise cards, the resolver sim, the Forces window, the planet view).
