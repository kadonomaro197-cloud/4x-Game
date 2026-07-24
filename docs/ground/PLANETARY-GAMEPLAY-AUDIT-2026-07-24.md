# Planetary Gameplay — Holistic Audit (2026-07-24)

**What this is:** a top-to-bottom read of *everything planetary* — the surface map, colonies, infrastructure,
surface minerals, ground combat, invasion, ground-unit design, and whether they connect into a loop a player can
actually live. Every claim below was checked against the **real code**, not the docs, then a second adversarial
pass tried to knock down every "it's built" grade to catch scaffolds graded too high. (Produced by an 8-lane
parallel audit + synthesis + verification; one grade — population — was knocked down and corrected here.)

**Read this before doing more planetary work.** It exists so we stop patching symptoms and start closing the loop.

---

## The one-paragraph truth

Planetary gameplay in this fork is a **remarkably complete cradle-to-grave machine that almost nobody can currently
reach or watch run.** The vertical chain — survey a world → settle it → mine and refine → build a warship *and* a
ground unit as real components → sail a fleet → win orbit → load and land troops → win the ground fight → flip the
planet's owner — **is built and wired end to end**, and CI proves the engine paths connect (`TakeAPlanetIntegrationTests`,
`OperationEarthfallTests`). That's a real milestone: the space-to-ground bridge that `PLAY-TO-MARS` named as *the*
great blocker is built and bolted to buttons. But three things keep it from being a game a player experiences:

1. **The front door is locked.** A default menu New Game spawns **no enemy, no garrison, no start fleet** (all three
   auto-flags default `false`), so the most-built system in the fork only lights up in the DevTest sandbox.
2. **The loop closes by bypassing its own softening step.** Orbital bombardment is orphaned from live fleet combat
   and from the AI, so capture works only because troops beat the garrison *on numbers*.
3. **None of it has ever run.** Every client and menu-gated link is compile-checked in CI but **runtime-unverified**
   on the developer's Windows build.

Underneath, the depth is real but uneven — the food→morale→migration spine and the infrastructure-efficiency
pressure loop genuinely *stack*, while whole levers sit **built-but-inert** (employment jobs = 0 in the data, power
demand = 0 everywhere, the located mineral hexes feed the map but not the mine). **Closing the reachability and
runtime gaps — not building more depth — is what turns this from a fidelity showcase into a playable stacking game.**

---

## Status board

| Subsystem | Maturity | Where it stands | The biggest single gap |
|---|---|---|---|
| **Surface map + terrain + survey/fog** | 🟢 Usable | One coherent Planet→Region→op-hex→mini-hex model; terrain flows; per-faction survey fog wired cradle-to-grave and rendered. | Two hex models still coexist (per-region disks + cylinder grid, against the G6 "delete the old one" rule); the **client** deposit fog is world-level + omniscient, not the per-faction masked assay the engine already computes. |
| **Population / morale / life-support** | 🟡 Usable-but-inert | Population *is* a real tank with morale as the level valve; food→morale→migration and the tax loop genuinely stack and reach industry via the crew draw. | **Employment lever is DARK in the data** (zero installation templates carry `EmploymentAtbDB` → jobs = 0 everywhere); **power sustenance is inert** (no consumption component → shortage = 0); the grave rung is incomplete (pop can hit 0 but nothing fires colony collapse). |
| **Infrastructure + buildings** | 🟢 Usable | Installations are real components; infra-efficiency is the one **fully-wired** economic pressure lever; start-colony buildings are located, capturable, bombable. | **Dynamic-build location hole** — installations built through the normal Production queue *after* game start are never placed onto a hex (no hook in `OnConstructionComplete`), so a grown colony's new buildings are invisible on the war map. The LOCKED PRINCIPLE silently fails for growth. |
| **Surface minerals + mining** | 🟠 Partial | Body-wide pool mining runs cradle-to-grave and is host-agnostic; the located-deposit hex layer is fully seeded + fog-gated. | **Mining reads ONLY the body-wide pool.** No mining code reads the `GroundHex` deposit — so the located hex layer is a *picture, not a participant*, and the surveyed tonnage has no accounting relationship to what a mine extracts. |
| **Ground combat** | 🟢 Usable | One auto-discovered hourly processor fights units on real-metre ranges through the shared `CombatKernel`, wired to sensors/fog, the AI, transport, and capture — genuinely not an island. | In a barebones menu game the layer is **live-but-empty** (`AutoRaiseHomeGarrison` defaults off); squad-as-N-models and biting ammo are dark in a stock fight. |
| **Invasion / space→ground** | 🟢 Usable | Transport embark/land, orbital-control gating, region + whole-planet capture — all built and wired to FleetWindow buttons + an AI `ConquerResolver` chain. The former MVP blocker is **closed**. | **Bombardment is orphaned** — the live fleet engine never fires on colonies, there's no `BombardOrder`/rung, so "soften before you land" is dark for the AI and a fiddly Fire-Control workaround for the player. |
| **Ground-unit design + assembly** | 🟢 Usable (engine) / 🔴 broken (client) | The universal Entity Assembler builds a ground unit as chassis + parts with carry/power/ammo gates biting, registers it buildable, and fields it — gauged end-to-end **in the engine**. | The **client** step is broken for humans: saved ground designs go to `IndustryDesigns` but the assembler lists only `ShipDesigns`, and the ground panels are gated behind first creating a ship design. Reachable only by a non-obvious workaround. |
| **The connected loop (survey→hold)** | 🟠 Partial | The whole loop connects cradle-to-grave as engine machinery and is CI-proven at the path level for both the player and the AI seat. | **Unreachable from the front door** (default game spawns no enemy/garrison/fleet) and **entirely runtime-unverified**. "Code exists" is at its widest distance from "a player can play it" right here. |

*(Adversarial-verify note: the population grade was knocked down from a clean "usable" — the morale/food/migration
spine is genuinely built and runs on a default game, but the employment and power levers are dark **in the data**,
so two of its advertised decision loops compute zero in a stock game.)*

---

## The connected loop — link by link, and the four places it breaks

**It closes as engine machinery.** Trace it:

- **SURVEY** *(built, strong)* — `GeoSurveyProcessor` reveals regions, generates the surface grid, seeds located
  deposits, grants per-faction fog with a two-tier assay.
- **SETTLE** *(built)* — the `SystemWindow` Colonize button, gated on survey-complete.
- **MINE** *(built — but NOT through the located layer)* — `MineResourcesProcessor` reads **only** the body-wide
  `MineralsDB` pool. The `GroundHex` deposits are a fog/picture layer mining never touches.
- **REFINE → BUILD** *(built, strong)* — infra-efficiency scales both mining and production. The one real economic
  pressure lever, fully wired.
- **BUILD GROUND UNIT** *(built, strong)* — Entity Assembler → `RegisterAssembledDesign` → `IndustryDesigns` →
  Production → `OnConstructionComplete` raises a `GroundUnit`. Gauged end-to-end.
- **LOAD / LAND** *(built — the former blocker is CLOSED)* — `LoadTroopsOrder`/`LandTroopsOrder` wired to FleetWindow
  buttons, gated on orbital control.
- **GROUND FIGHT + CAPTURE** *(built)* — shared `CombatKernel` resolver → region owner flip → `TryCapturePlanet`
  flips `FactionOwnerID`.

**But it breaks in four named places — none of which CI catches, because they're reachability / data / runtime
gaps, not compile gaps:**

**BREAK 1 — THE FRONT DOOR (the biggest).** A default menu New Game has nothing to fight. `AutoSpawnCombatScenario`
/ `AutoRaiseHomeGarrison` / `AutoBuildStartFleets` all default **false** (`NewGameMenu.cs`). All conquest content
lives on `DevTestStartFactory`. A player does New Game, builds a whole economy and army, and finds no enemy, no
garrison, no start ships.

**BREAK 2 — BOMBARDMENT IS ORPHANED.** The auto-resolve fleet engine never fires on colonies. There's no
`BombardColonyOrder`, no client button, no `ConquerResolver` bombard rung. "Soften the beach" is dark for the AI and
reachable only by manually aiming beam Fire Control at the colony. The loop closes anyway because troops-by-numbers
beat the garrison — the softening step is *bypassed, not satisfied*.

**BREAK 3 — THE ASSEMBLER CAN'T BE ENTERED FOR GROUND.** A saved ground design is written to `IndustryDesigns` but
the designer lists only `ShipDesigns`, and the ground panels are gated behind having an existing ship design. A
human can design a ground unit only via a non-obvious workaround.

**BREAK 4 — RUNTIME IS UNVERIFIED.** Every client-facing and menu-gated link is compile-checked only. The loop
connects on paper and in the source; whether it runs cradle-to-grave in one live sitting is unproven.

**Built-but-disconnected ISLANDS:** (a) the located-deposit hex layer (feeds the map + fog, feeds *nothing* in
mining); (b) the employment morale lever (zero templates carry it → jobs = 0); (c) power sustenance (no consumption
component → shortage = 0); (d) organically-built installations (never located onto a hex → a grown colony's new
mines/factories are invisible on the war map); (e) the whole ground-tactical depth (real-metre ranges, tactical AI)
— live but fighting over an empty field in a stock game.

---

## Cross-cutting themes (the patterns worth naming)

1. **Built-but-unreachable front door** — the single most-repeated finding. The deepest, most-connected system in
   the fork only lights up in DevTest. *Depth was built; the door to it was never opened.*
2. **Default-off / default-zero flags and data** — real levers ship inert (employment jobs = 0, power demand = 0,
   sustenance only bites in authored "strain" nodes, ammo only on player-designed magazine units). The machinery is
   built and tested; the *data/flag that makes it fire* is missing.
3. **Cosmetic-vs-source-of-truth split** — the located hex layer (deposits, fog) is a beautiful *picture* layered
   over an economy that still runs on the old body-wide aggregates. Two representations of the same fact, only one
   load-bearing.
4. **Two representations kept past the deadline** — the dual hex model (disks + cylinder grid) and the dual mineral
   truth (pool vs hex). Migration started, the old representation not yet deleted, consumers straddle both.
5. **Located-at-creation-only** — capabilities are wired at an entity's *birth* but not at the recurring event
   (installations locate at colony creation but not at production completion).
6. **CI-blind runtime** — every client render, click path, and menu-gated behavior is compile-checked but never run.
   The most consequential gaps are exactly the ones CI structurally cannot see.
7. **Grave rung stops short** — capture and death flip an ownership *flag* but don't transfer the *substance*
   (`TryCapturePlanet` is a bare `FactionOwnerID` flip; pop→0 fires nothing; Rebellion has no resolution). "Break
   their morale to take the world" — the stated invasion objective — is not yet reachable as a consequence.
8. **Doc drift on the frontier** — the highest-traffic status docs (`PLAY-TO-MARS`, `MVP`) still name blockers that
   are now built (load/land UI, unit designer) while omitting the ones that are actually live (no menu enemy, no
   bombard order). A cold reader is steered at the wrong gaps.

---

## The roadmap — how to get where we need to be

Ordered by "nothing downstream matters until this is done." The theme: **stop building depth, start closing
reachability + runtime.**

### P1 — Open the front door *(medium)*
**Goal:** a default menu New Game presents a takeable target, so the built loop is reachable without DevTools.
- Give the menu path a takeable enemy: default `AutoRaiseHomeGarrison` on for at least one rival body, **or** wire a
  minimal NPC colony+garrison into `CreateGameCore` (not only `DevTestStartFactory`).
- Ensure the player starts with (or can quickly build) a fleet + transport so the bridge has something to sail.
- Guard the new default with a `BaseModIntegrityTest`-style gauge so the menu path stays populated.
- **Unblocks:** turns the most-built system in the fork from DevTools-only into something a player meets on New Game
  — the precondition for every runtime test below.

### P2 — Prove it runs once, live *(medium)*
**Goal:** one recorded cradle-to-grave play-test on the developer's Windows build: survey → colonize → mine → build
a unit → load → sail → win orbit → land → capture.
- Run the full chain in a menu game with AI gates on, capturing `console_output.txt` + gauge readings at each step.
- Add the steps as rows in `CLIENT-TEST-CHECKLIST.md` / `TESTING-TRACKER.md`.
- Feed any runtime crash/NaN/freeze back for a fix cycle; repeat until the loop closes live once.
- **Unblocks:** converts "the loop connects in source" into "the loop plays" — the missing runtime proof CI can't
  provide. Everything downstream is deepening an unproven system until this fires.

### P3 — Close the bombardment joint *(medium)*
**Goal:** "soften before you land" becomes a real emitted decision for both seats, not a Fire-Control accident.
- Add a first-class **region-targeted `BombardColonyOrder`** + client button that routes into the already-wired
  `DamageProcessor.ApplyGroundBombardment` path.
- Add a `ConquerResolver` bombard rung **above** the LAND rung so the AI softens the beach too.
- Make bombardment region-targeted (hit the landing region's defenders, not the whole surface).
- Calibrate `GroundBombardmentDamagePerStrength` + the colony energy divisor once warhead energies lock.
- **Unblocks:** removes the one orphaned link in the invasion chain and makes the space fleet matter to the ground
  outcome — the standoff-vs-brawl softening decision the design wants.

### P4 — Make the inert levers bite *(medium, mostly data)*
**Goal:** the built morale/economy levers actually fire in a stock game instead of computing zero.
- Add `EmploymentAtbDB` (Jobs) to the productive installation templates (mine/factory/refinery/lab/shipyard),
  guarded by `BaseModIntegrityTests`, so `GetTotalJobs() > 0` and the two-sided employment morale lever fires.
- Build the power rung: a per-capita power demand so `PowerShortage` bites like food.
- Field a base-mod ammo-fed ground unit so the built ammo-drain/resupply loop bites in a stock fight.
- **Unblocks:** turns three built-but-dead decision loops into live pressure — the difference between a fidelity
  showcase and a decision engine, cheaply.

### P5 — Fix the client fog + assembler entry *(medium)*
**Goal:** the player sees what the engine already computed, and can design a ground unit without a workaround.
- Migrate client deposit fog to **per-faction** (read the region's per-faction reveal + the masked assay), unifying
  the two survey gates in `PlanetViewWindow` onto one source — closes the assay leak the engine already fixed.
- In the Entity Assembler, list `GroundUnitDesigns` from `IndustryDesigns` alongside `ShipDesigns`, and let the
  ground panels render without a pre-existing ship design.
- Runtime-verify on the developer's build.
- **Unblocks:** the last two player-reachability breaks.

### P6 — Locate organic growth + complete the grave rung *(large)*
**Goal:** a colony that grows over time stays fully on the war map; capturing/breaking a world transfers substance.
- Location hook at production completion: `OnConstructionComplete` calls `LocateColonyInstallations` +
  `LocateFootprintsOnHexes` (both idempotent) so organically-built installations land on the war map.
- Extend infra-efficiency to resolve a station's body via `StationInfoDB.HostingBodyEntity`.
- Wire the depopulation/legitimacy grave rung: on Rebellion window-expiry or pop→0, fire a colony-collapse /
  ownership-change event.
- Deepen `TryCapturePlanet` beyond an ownership flip — handle population, installations, and economy on capture.
- **Unblocks:** the LOCKED PRINCIPLE holds for growth (not just the start layout), and "break their morale to take
  the world" becomes reachable.

### P7 — Collapse the dual representations *(large)*
**Goal:** one hex model and one mineral source of truth, as the designs require.
- Finish G6b: move tactical micro onto the global cylinder grid, then delete per-region disk gen + dual coords.
- Promote located hex deposits to the mined source of truth: a mine draws from and depletes the `GroundHex` it sits
  on; reconcile the located-fraction accounting; add a CI gauge that mines a deposit hex and asserts THAT hex falls.
- **Unblocks:** pays down the two biggest coherence debts the designs explicitly forbid keeping — makes the map the
  source of truth, not a picture.

---

## Top priorities (the ranked next moves)

1. **Open the front door** (P1) — nothing else matters until a player can *meet* the loop.
2. **Run one live cradle-to-grave play-test** (P2) — the missing runtime proof for the entire ground/invasion stack.
3. **Add the `BombardColonyOrder` + rung** (P3) — make "soften before you land" a real decision for both seats.
4. **Make the inert levers bite in data** (P4) — employment jobs + power demand; cheap, mostly JSON, high payoff.
5. **Fix the two client breaks** (P5) — per-faction deposit fog + ground-design entry in the assembler.

---

## Doc cleanup (owed in the same spirit)

- **`docs/PLAY-TO-MARS-WALKTHROUGH.md`** — STALE. Flip the load/land + custom-ground-unit-designer gaps to DONE (the
  FleetWindow C5.1 / Entity Assembler work); keep "no menu enemy" + "no bombard order" as the live remaining breaks.
- **`docs/MVP.md`** — STALE. The "client invade panel is the one true remaining v1 gap" claim is superseded (the
  Embark/Land UI exists). Re-point "the one true remaining v1 gap" to: a normal-game target + a live runtime pass.
- **`docs/ground/GROUND-UNIT-VARIABLES.md`** — STALE resolver line refs + a "weapons flatten" finding superseded by
  the built per-weapon loadout.
- **`docs/SYSTEMS-STATUS-AND-TEST-PLAN.md`** — being retired; migrate live rows into DOCS-INDEX / TESTING-TRACKER /
  SYSTEM-CONNECTION-MAP and mark it superseded.
- **`Pulsar4X/Pulsar4X.Client/CLAUDE.md`** — document the two client Entity-Assembler ground breaks so the next
  session doesn't assume the ground designer is reachable.
- **`docs/DOCS-INDEX.md`** — flip the rows above; **`docs/TESTING-TRACKER.md`** — add the runtime-unverified links
  this audit surfaced (the one-sitting play-test; morale/tax/starvation readouts move; assembler ground flow;
  bombard removes a building from the economy readout).

---

## Bottom line

We are **not** short on planetary *depth*. We're short on two things: a **player being able to reach it** (open the
front door, fix the two client breaks) and **proof it runs** (one live cradle-to-grave sitting). Do P1–P2 first and
the fork crosses from "a fidelity showcase that connects in source" to "a planetary loop you can play." P3–P5 make
that loop a *decision engine* (softening, live morale levers). P6–P7 pay down the coherence debt so growth and the
map stay honest. **The order matters more than the count.**
