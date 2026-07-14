# Carrier Design — force projection from standoff range

**Status:** design proposal (NOT locked). First pass, 2026-07-14. Prime-Directive investigation done against the live combat/weapons/ship/bay code; the EXISTS ledger below is file-cited. Read `docs/combat/COMBAT-DESIGN.md` (the one auto-resolve loop + doctrine) and `docs/combat/WEAPONS-DESIGN.md` (the weapon triangle) first — carriers plug into both, they don't replace either.

---

## 1. The decision first — what a carrier ADDS to the game

Space combat here runs itself: two fleets close, the math fights it out, the player sets **doctrine** (the standing battle orders — press / hold / screen / withdraw). The whole game of combat is the **standoff-vs-brawl** choice (`COMBAT-DESIGN.md`). A carrier is a **new answer to that choice, and a new axis on it.**

**A gun-line brawls.** It closes to weapon range, trades fire, and the tanky, well-armed hull wins. **A carrier does not.** It sits *beyond* gun range and throws a swarm of small, hard-to-hit strike craft at the enemy — projecting force it never has to expose. Think of the difference between a battleship steaming into a gunfight and a flattop launching an air wing from over the horizon. Same fleet fight; completely different bet.

The bet has teeth on both sides — which is what makes it a real decision, not a strictly-better toy:
- **The upside:** you hit from range, your strike craft are evasive (the weapon-triangle's dodge corner), and you keep your expensive hull out of the brawl.
- **The downside:** the carrier itself is a **soft, high-value target** — if a fast brawler closes on it, it dies quickly and cheaply; AND strike-craft swarms are exactly what **flak and point-defense** are built to shred (a swarm is a high-*saturation* target, and saturation weapons floor evasion — `WEAPONS-DESIGN.md`). So a carrier group *forces* the enemy toward flak-heavy escorts, and can be hard-countered by them.

**That is the decision:** build a gun-line (direct, tanky, brawls) **or** a carrier group (projects evasive strike from range, but soft if caught and hard-countered by saturation defense). It stacks on top of the systems already in the game — detection (you must *see* to project at range), the weapon triangle (fighters vs. flak), doctrine (standoff posture), and hull/mass budget (a flight deck is heavy). **Name the decision before the realism** (`docs/REALISM-VS-GAMEPLAY-AUDIT.md`): the decision is *project-from-range vs. close-and-brawl*, and *how much flak escort* the enemy must invest to answer you.

---

## 2. How it fits the ONE combat loop (no new engine)

**Carriers do NOT need a second combat engine.** This is the load-bearing insight and the reason the feature is affordable:

- There is one math loop — the **auto-resolver** — and it aggregates fire by weapon class so a 1000-ship battle costs the same as a 5-ship one (`COMBAT-DESIGN.md` Tier 0; `CombatKernel`).
- A launched strike-craft wing is just **more evasive firepower in that same salvo math.** A fighter already resolves as a ship with high `Evasion` (small volume + high acceleration → `ShipCombatValueDB.Evasion`, capped 0.95) carrying the dodge-corner of the triangle. A carrier's wing is that, projected from a platform instead of flown in as fleet members.
- The **counter already exists**: `PointDefenseAtb` (intercepts saturation fire) + the flak/saturation corner. Nothing new to build to *answer* a carrier — the triangle absorbs it.

So a carrier is a **standoff sub-fleet role** (the Rear-Guard / Artillery family in the sub-fleet doctrine, `COMBAT-DESIGN.md` System 4) that, each combat pulse, feeds a wing of evasive firepower into the aggregate resolve and holds range. The player's decision surfaces as **doctrine** (stand off and launch) + **design** (how big a deck, what craft) — exactly the existing control surface.

---

## 3. EXISTS / MISSING / NEEDS-CHANGE (the Prime-Directive ledger)

**EXISTS — reuse, don't rebuild:**
| Piece | Where | Reuse as |
|---|---|---|
| The **bay pattern** — a ship carries units in bays | `GroundBayAtb` (Capacity + CarryClass), `GroundTransportDB` (carried list on the ship), `GroundTransport` (load/land/capacity statics) | the **exact template** for a hangar: `HangarBayAtb` mirrors `GroundBayAtb`; `CarrierDB` mirrors `GroundTransportDB`; a `CarrierOps` static mirrors `GroundTransport`. A carrier carries *fighters* the way a trooper carries *infantry*. |
| **Ship combat value + evasion** | `ShipCombatValueDB` (Firepower/Toughness/Evasion, `EvasionCap` 0.95) | a strike-craft wing's firepower + dodge — already computed for small hulls |
| **The weapon triangle** | `WeaponProfile`/`WeaponClassifier` (position emerges from Velocity/Saturation/Tracking) | fighters = the evasive corner; **flak/PD = the counter** — no new counter needed |
| **Point defense** | `PointDefenseAtb` (InterceptRating_Jps), saturation math | the thing that shreds a strike wing (already the missile counter) |
| **Launch-an-autonomous-craft mechanic** | `OrdnanceDesign` (a buildable, cargo-carried, launched design) + `MissileLauncherAtb` + `MissileProcessor` | the *reference* for the "expendable" modelling alt (§9 decision), and for build/stock/launch plumbing |
| **Parent/child entity hierarchy** | `TreeHierarchyDB` (ParentDB/Root — fleets nest on it) | if craft ever become real entities, they parent to the carrier |
| **The shared ship designer** | ship designs (`default-ship-design-test-fighter` is already the tiny evasive corner) | strike craft are just small ship designs — **the craft cradle-to-grave already exists** |
| **Mass budget / hull tiers** | `ShipDesign.MassBudget`, `ship-hull` light/medium/heavy | a flight deck is heavy → forces a big hull (the design trade) |

**MISSING — the new build:**
- `HangarBayAtb` — the flight-deck component attribute (the dials, §5).
- `CarrierDB` — the "what's aboard / what's aloft" state on the carrier ship (mirrors `GroundTransportDB`).
- `CarrierOps` static — launch / recover / sortie-tempo / capacity (mirrors `GroundTransport`).
- The **resolver hook** — launched wing strength joins the aggregate salvo (the one real combat-engine touch).
- Base-mod data — a `flight-deck` template + a `strike-craft` (fighter) + `strike-bomber` design + a carrier hull.

**NEEDS-CHANGE:**
- The auto-resolve `StepEngagement` gains a pre-salvo step: each carrier launches up to its sortie rate into the side's fire pool, and attrited craft come off the carrier's wing pool. Small, aggregate, O(carriers) — keeps the bucketed-resolve performance guarantee.

---

## 4. The two cradle-to-grave chains

Carriers are really **two** cradle-to-grave capabilities that meet at the hangar. Both must be reachable through the whole chain (`CLAUDE.md` "Cradle to Grave").

### 4a. The CARRIER (the flight-deck component)
> **mineral** (mined) → **material** (refined — a deck is steel + electronics) → **flight-deck component** (`HangarBayAtb`, designed in the shared component designer with its dials) → gated by **research** (Carrier Operations tech → bigger decks / faster sorties) → installed on a **heavy hull** (the deck's mass forces a big, soft hull — the design trade) → the **in-combat decision** (stand off · launch a strike · hold Combat Air Patrol · recover & rearm) → **destroyed** (a hit deck stops flight ops — the carrier goes dark; the hull is a soft HVU; craft caught aloft with no deck to recover to are stranded and lost). The grave rung wires the carrier to the damage system: *knock out the deck and the air wing is useless.*

### 4b. The STRIKE CRAFT (the air wing) — **already exists as a chain**
> **mineral → material → components** (a small hull + a weapon + an engine) → **research** (better fighters/bombers) → designed in the **shared ship designer** (`default-ship-design-test-fighter` today — evasion emerges from small volume + high thrust) → built in **industry** → **based in the hangar** (a bay-load, like troops in a trooper) → the **decision** (which craft to embark: nimble anti-ship *fighter* vs. slow anti-capital *bomber*) → **lost in combat** (attrited from the wing pool; you re-build and re-embark). Because a strike craft is *just a ship design*, it gets research-gating, construction-from-materials, save/load, and the design UI **for free** — this is `CONVENTIONS.md` §6 (abilities are components) paying off.

---

## 5. The components and their dials (the ask)

### The Flight Deck / Hangar Bay — `HangarBayAtb` (mirror of `GroundBayAtb`)
The one new load-bearing component. Its dials are the carrier's whole character (all subject to the mass/research trade so they're *earned*, per the free-dial audit — dialing a bigger deck costs mass/research/materials):

| Dial | What it controls | Analogy |
|---|---|---|
| **Bay Capacity** (craft-tonnage) | how big an air wing the carrier holds | the size of the hangar deck |
| **Sortie Rate** (craft-tons launched per combat pulse) | launch *tempo* — how fast you put strike into the fight | catapult throughput / deck-crew cycle time |
| **Recovery / Rearm Rate** (craft-tons recovered+readied per pulse) | how fast attrited/returning craft refuel, rearm, relaunch | arrestor gear + turnaround crew |
| **Craft Class supported** (a `CarryClass`: Light / Heavy) | can this deck cycle only light fighters, or heavy bombers too | deck strength / elevator size |
| *(fold-in)* **Deck Vulnerability** | a deck hit's chance to suppress ops (the grave rung) | armored vs. open flight deck |

> **Scope call (don't over-model):** fold launch catapult, arrestor gear, and deck crew into **Sortie Rate / Recovery Rate** rather than separate components. One flight-deck component with four dials beats five fiddly parts nobody tunes. (Matches the "trim the pretty" rule.)

### The Strike Craft — the shared ship designer (exists)
No new component system — a strike craft is a small ship design:
- **Fighter** — tiny hull + light guns + high-thrust engine → high evasion, anti-ship. The dodge corner.
- **Bomber** — bigger small-hull + an anti-capital payload (heavy ordnance / a torpedo = an `OrdnanceDesign`) + less thrust → less evasion, hits hard on capitals, wants a fighter screen.
The *nature × delivery* of the craft's weapon decides where it sits in the triangle — same rules as any ship.

### The Escort (the counter — also exists)
A **flak / point-defense escort** (`PointDefenseAtb`, flak profiles) is the answer to a carrier. Not new — it's the flak corner of the triangle. Design surfaces this as: *a carrier group forces the enemy to spend hull budget on flak escorts.*

---

## 6. How it operates in a battle (the pulse loop)

Inside the one auto-resolve loop, per combat pulse:
1. **Stand off.** The carrier's doctrine holds it at range (Rear-Guard/Standoff role). It contributes little/no direct-fire itself.
2. **Launch.** It moves up to **Sortie Rate** craft-tons from its **Bay Capacity** pool into the side's *aloft wing*.
3. **The wing fights** as aggregate evasive firepower in the normal salvo math — it deals fighter-firepower and takes enemy fire at fighter-evasion. **Flak/PD on the enemy side floors that evasion** (saturation) and attrites the wing fast; light guns barely scratch it.
4. **Attrition** comes off the aloft wing pool (craft-tons lost).
5. **Recover & rearm.** Surviving craft cycle back at **Recovery Rate** into the bay, ready to relaunch — the carrier's sustain.
6. **Grave rungs:** if the **carrier dies**, its aloft wing is stranded (no deck to recover to) and bleeds out; if the **deck is suppressed** (a deck hit), launch/recovery stop even though the hull lives.

The whole thing is a handful of pool arithmetic per carrier per pulse — **O(carriers)**, no per-fighter entities — so it respects the "1000-ship battle is cheap" guarantee. The *feel* (a wing melting under flak, a carrier caught and gutted) falls out of the existing triangle + evasion + PD math.

---

## 7. Connection map (map the connections, then watch them move)

- **Feeds INTO:** the auto-resolver (wing strength → the salvo pool); the fleet doctrine (a Carrier/Standoff sub-fleet role); the damage system (deck-suppression + carrier-death → wing loss).
- **Feeds FROM:** the ship designer (strike craft + the heavy hull); research (Carrier Ops tech); industry (build deck + craft + escorts); **detection** (standoff force projection *requires* seeing the enemy first — carriers and `DETECTION-DESIGN.md` are joined at the hip; a blind carrier can't project).
- **Shares STATE with:** the **bay pattern** — `HangarBayAtb`/`CarrierDB` are the literal twins of `GroundBayAtb`/`GroundTransportDB`. Bug-fix or improve one, check the other.
- **TRIGGERS:** launch/recover events; PD/flak interception of the wing; carrier-death → strand the wing; the enemy's *doctrine response* (bring flak escorts).

The **Connect win:** the counter is already in the box. Carriers don't need a bespoke defense — they slot into the existing rock-paper-scissors (carrier-wing ▸ soft gun-line at range; **flak/PD ▸ carrier-wing**; fast-brawler ▸ soft carrier). That's the triangle earning its keep.

---

## 8. Balance & anti-degenerate notes

- **Not strictly-better than a gun-line:** the carrier is soft, hard-countered by flak/PD (which the enemy will bring once carriers appear), and useless without detection. Its dials cost mass/research (a big deck = a big soft hull).
- **Sortie/Recovery rate is the tempo knob** — the anti-"alpha-strike-wins" valve. A carrier that dumps its whole wing in one pulse is fragile to a flak wall; drip-feeding sustains but hits softer. That's a real doctrine choice.
- **Bombers want a screen** — anti-capital craft are less evasive, so a pure-bomber wing melts to flak; mixing fighters (screen) + bombers (punch) is the combined-arms play. Emerges from the triangle, not a hardcoded rule.

---

## 9. Open decisions for the developer (weigh in before C-build)

1. **Strike craft = recoverable SHIPS-in-a-bay, or expendable ORDNANCE (big reusable missiles)?**
   **Recommend SHIPS-in-a-bay.** It's the real carrier fantasy (embark, sortie, recover, rearm, attrit), it reuses the *most* existing systems (the bay pattern + ship combat value + the weapon triangle + the designer), and recover/rearm is the carrier's whole identity. The ordnance route (model a "strike" as a launched `OrdnanceDesign`) is simpler but throws away the recover/sustain loop and makes a carrier just a missile boat. *(Alt kept on the shelf, not chosen.)*

2. **Do launched craft persist as entities, or resolve as an aggregate pool?**
   **Recommend AGGREGATE (a wing-strength pool the carrier spends/attrits).** Per-fighter entities would blow the bucketed-resolve performance budget (the thing that makes 1000-ship battles cheap). The bay holds N craft-tons; combat spends/attrits that pool; recovery refills it. Individual craft can be a *visual skin* later, never the source of truth — exactly the Tier-0/Tier-2 split the combat doc already draws.

3. **The carrier's doctrine role** — add a **Carrier/Standoff** posture to the sub-fleet doctrine catalog (Rear-Guard/Artillery family): hold range, launch, don't close. Data-only (a `CombatDoctrineBlueprint` row).

4. **Scope of the deck component** — one `flight-deck` template with the four dials (§5), NOT separate catapult/arrestor/deck-crew parts. Confirm the trim.

---

## 10. Proposed build sequence (each a gauged slice, CI-verified, one at a time)

- **C1 — the component (data + atb).** `HangarBayAtb` (mirror `GroundBayAtb`) + a base-mod `flight-deck` template (JSON→atb, the gotcha-#10 sensor) + a `default-design-flight-deck` + a carrier hull design. Gauge: the deck binds its dials from JSON and mounts on a ship (like `GroundUnitBaseModTests`). *Byte-identical to combat — nothing launches yet.*
- **C2 — the carry state + ops (engine, mirror `GroundTransport`).** `CarrierDB` (aboard/aloft pools) + `CarrierOps` (Embark / Launch / Recover / Capacity). Gauge: embark craft, launch up to sortie rate, recover — pool arithmetic pinned in a test. *Still byte-identical to the live battle (unwired).*
- **C3 — the resolver hook.** `StepEngagement` gains the pre-salvo launch + wing-attrition + recovery step. Gauge: a carrier + fighter wing out-damages nothing on its own but, vs. a soft gun-line at range, wins; vs. a **flak escort**, the wing melts and the carrier loses — the triangle payoff, through the real resolve. *This is the one real combat change; gate it if needed for byte-identity.*
- **C4 — doctrine + AI.** The Carrier/Standoff posture (data) + the NPC brain fields a carrier group when its doctrine tilts that way. Gauge: doctrine applies; AI builds deck+craft.
- **C5 — bombers + anti-capital craft.** A bomber strike-craft (heavy payload, needs a fighter screen) — the combined-arms layer. Gauge: bomber melts to flak alone, survives with a fighter screen.

Each slice: design the decision → build → **gauge in CI** → wait green → next. The counters (flak/PD) already exist, so the very first playable slice (C1–C3) is a *complete* rock-paper-scissors, not a half-system.

---

## 11. One-paragraph summary (the point, restated)

A carrier is a **standoff force-projector**: it throws a swarm of evasive strike craft from beyond gun range, trading a soft, expensive hull and hard-countered-by-flak fragility for reach and dodge. It needs **no new combat engine** — launched craft are evasive firepower in the one auto-resolve loop, the **hangar is the troop-bay pattern reused** (`HangarBayAtb`/`CarrierDB`/`CarrierOps` mirror the ground versions), the **strike craft are just small ship designs** (the whole craft cradle-to-grave already exists), and the **counter is already in the box** (flak/point-defense shred saturation-heavy evasive swarms). The new build is one component with four dials (Bay Capacity · Sortie Rate · Recovery Rate · Craft Class), a carry-state blob, and one pre-salvo resolver hook. The decision it adds — *project from range vs. close and brawl, and how much flak the enemy must buy to answer you* — stacks cleanly on detection, the weapon triangle, doctrine, and the mass budget.
