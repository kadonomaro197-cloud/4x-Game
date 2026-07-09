# Designer Audit — index

A complete, file:line-cited survey of **everything a player can design, make, or build in Pulsar4X**, produced to fix the developer's finding that *the in-game designers are not universal* (a part designed for one host — ship / station / colony installation / ground unit — can't be freely reused on another). Produced 2026-07-08 by a 7-way parallel code audit on branch `claude/sol-playtest-earth-map-8r59j6`.

## Start here
- **`00-EXECUTIVE-SUMMARY.md`** — the generalized model (everything a player makes is one ladder: mineral → material → component → assembly), the two-lock diagnosis (mount flag vs processor reader), what's *already* universal (the fix templates), and the **ranked fix plan** (4 tiers). Read this first; the rest is evidence.

## The seven evidence sections
1. **`01-DESIGNER-UIS.md`** — every designer window in the client; the Component Designer is the one universal one; the 4 assemblers each hardcode a single mount flag.
2. **`02-DESIGNABLE-TYPES.md`** — the 5 buildable C# types (`ComponentDesign`, `ShipDesign`, `OrdnanceDesign`, `GroundUnitDesign`, `ProcessedMaterial`); mount legality lives only on `ComponentDesign`; no shared assembly base.
3. **`03-ABILITIES-AND-MOUNTS.md`** — **the crux.** Every `IComponentDesignAttribute`, the processor that reads it, the host it therefore works on, and the **9 parallel/duplicated ability pairs**. The reactor (`EnergyGenerationAtb`) is the one already-universal counter-example.
4. **`04-BASEMOD-TEMPLATES.md`** — all 67 base-mod component templates categorized; the mount-flag data inconsistencies (solar array, spaceport dup, weapons-vs-sensors).
5. **`05-ASSEMBLIES.md`** — how ships / stations / ground units / missiles are composed; 4 triplicated assemblers; `ComponentMountType` honored by only ~1.5 of 4 paths.
6. **`06-INDUSTRY-AND-MATERIALS.md`** — the production/economy layer; **already fully host-uniform** — the pattern the designers should mirror.
7. **`07-RESEARCH-AND-UNLOCKS.md`** — tech gating and template availability; **already fully host-uniform** — not the source of the bug.

## The finding in one line
The universality is real at the bottom of the ladder (a component + the industry that builds it don't care about host) and lost in the middle band (which designer offers a part, which validator admits it, and whether the ability's *reader* runs on that host) — because that middle band was built four times, once per host.
