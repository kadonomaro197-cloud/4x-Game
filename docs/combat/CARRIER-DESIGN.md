# Carrier Design — force projection from standoff range

**Status:** **OFFICIAL / design-locked, ready to build** (2026-07-14). The four §9 decisions are made, the bay class includes Shuttle, and the full C1 blast radius is mapped and verified tractable (§12). Prime-Directive investigation done against the live combat/weapons/ship/bay code; the EXISTS ledger + the §12 touch-list are file-cited. Read `docs/combat/COMBAT-DESIGN.md` (the one auto-resolve loop + doctrine) and `docs/combat/WEAPONS-DESIGN.md` (the weapon triangle) first — carriers plug into both, they don't replace either.

> **The four locked decisions (developer, 2026-07-14), driving everything below:**
> 1. **Strike craft are real built entities** (built like troops, through industry), held in the carrier as a **wing pool** (the hangar inventory), and **launched as a SUB-FLEET** that fights via the normal fleet auto-resolve.
> 2. **Doctrine:** the **carrier's own doctrine AND its launched sub-fleet's doctrine SUPERSEDE the parent fleet's** (the existing sub-fleet-overrides-parent model). Add a **Carrier/Standoff** doctrine role.
> 3. **One** flight-deck component (not separate catapult/arrestor/crew parts).
> 4. **GENERALIZE the bay:** the flight deck is the SAME component as a **troop bay**, a **personal (passenger) carrier**, and a **shuttle bay** — one generalized bay component with a *class* dial (troops / vehicles / strike craft / passengers / **shuttles**), per `CONVENTIONS.md` §6 (don't build parallel systems). This *replaces* the design's earlier "aggregate pool" idea (decision 1 above) and the ground-only `GroundBayAtb`.

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
- **Generalize `GroundBayAtb → BayAtb`** (add `BayClass.StrikeCraft`/`Passenger` + a `Cycle Rate` dial) — the flight deck is this component at `BayClass.StrikeCraft` (§5). *Not a new component — a promotion of the existing one.*
- `CarrierDB` — the "what's aboard (wing pool) / what's aloft (sub-fleet)" state on the carrier ship (the `StrikeCraft`-class twin of `GroundTransportDB`).
- `CarrierOps` static — launch-as-sub-fleet / recover / capacity (the `StrikeCraft` twin of `GroundTransport`).
- The **resolver hook** — per-pulse launch/recover; the launched sub-fleet fights on the normal resolve (the one real combat-engine touch).
- Base-mod data — a `flight-deck` template (`BayAtb{StrikeCraft}`) + a `strike-craft` (fighter) + `strike-bomber` design + a carrier hull.

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

### The GENERALIZED BAY — one component, four cargoes (decision 4)

**The load-bearing design move: there is ONE bay component, not a separate troop bay and flight deck.** The developer's call — a flight deck, a troop bay, and a passenger liner's berths are *the same thing*: a hosted space that carries units and cycles them in/out. So we **promote the existing ground-only `GroundBayAtb` into a general `BayAtb`** and dial its *class*. This is `CONVENTIONS.md` §6 (abilities are components; don't invent parallel systems) applied to carrying-capacity — and it's why a carrier is cheap: the troop-transport machinery **already exists** and the carrier is the same component pointed at a different cargo.

**`BayAtb`** (generalizes `GroundBayAtb` — same shape, one new dial + a wider class enum). All dials ride the mass/research trade so a bigger/faster bay is *earned* (per the free-dial audit):

| Dial | What it controls | Deck analogy / troop-bay analogy |
|---|---|---|
| **Capacity** (carry-size units) | how much the bay holds — an air wing, a battalion, a passenger complement | hangar size / hold size |
| **Bay Class** (the cargo it's fitted for) | **Personnel** (troops) · **Vehicle** (armour/artillery) · **StrikeCraft** (fighters/bombers) · **Passenger** (colonists/VIPs) · **Shuttle** (utility transport craft — surface↔orbit / ship↔ship ferrying) | deck-strength / berth-type — *a bay carries only its own class* (you can't cram a tank in a troop bay, or a fighter in a passenger berth) |
| **Cycle Rate** (carry-size moved in/out per operational pulse) | the tempo: **Sortie Rate** for a flight deck, **assault-deploy rate** for a troop bay, **boarding rate** for passengers, **turnaround rate** for shuttles | catapult+arrestor throughput / ramp-and-crew speed |

> **Enum ordinal stability (build note):** `BayClass` keeps `Personnel = 0`, `Vehicle = 1` (the existing `GroundCarryClass` values the frame JSON already encodes), then appends `StrikeCraft = 2`, `Passenger = 3`, `Shuttle = 4` — so every existing base-mod frame + troop-bay stays byte-identical through the generalization.

> The old design's four separate deck dials collapse into these three: **Sortie Rate + Recovery Rate → one Cycle Rate** (launch and recover are the same "move craft across the deck" throughput), **Craft Class → the Bay Class enum** (now spanning all four cargoes), **Deck Vulnerability → the existing grave rung** (a bay shot off a ship already takes its contents with it — `GroundBayAtb`'s documented behaviour, inherited).

**Same component, four products (all just `BayAtb` at a different class):**
- **Troop transport** = `BayAtb{ Personnel|Vehicle }` — *exists today* (the trooper/dropship). Migrates onto the general component byte-identically.
- **Carrier** = `BayAtb{ StrikeCraft }` + a fast Cycle Rate — the flight deck.
- **Personal carrier / liner** = `BayAtb{ Passenger }` — colonist/VIP lift (a future consumer; the component is ready for it).
- **Shuttle carrier** = `BayAtb{ Shuttle }` — holds utility transport craft that ferry units/cargo/personnel surface↔orbit and ship↔ship (a dropship's landers, a station's shuttle complement). *(Distinct from the existing `cargo-Shuttlebay`, which is a `CargoTransferAtb` cargo/fuel-transfer ability, not a unit bay — a candidate to FOLD INTO this class later, see §12.)*

**What differs per class is the CARGO + the deploy handler, not the bay:**
| Bay Class | Carried-state | Deploy logic | Status |
|---|---|---|---|
| Personnel / Vehicle | `GroundTransportDB.LoadedUnits` (ground-unit records) | `GroundTransport` (load at colony / land on target) | **EXISTS** |
| StrikeCraft | `CarrierDB` (aboard **wing pool** + aloft **sub-fleet**) | `CarrierOps` (launch a sub-fleet / recover) | **NEW** |
| Shuttle | a shuttle-craft manifest (like `CarrierDB` but non-combat) | launch/recover ferry runs (reuses `CarrierOps`) | future |
| Passenger | a passenger manifest | boarding/disembark transfer | future |

So the **component generalizes; the cargo handler is per-class.** One physical bay, class-specific contents — exactly "reuse the logic, generalize the component."

> **Migration note (Prime Directive / landmine L3):** `GroundBayAtb` is bound from the troop-bay JSON template and stored on existing ship designs; `TypeNameHandling` embeds its class name, so promoting `GroundBayAtb → BayAtb` (and `GroundCarryClass → BayClass`) is a save/design-format change. The DevTest is pre-release (throwaway saves), so this is acceptable **if done as a clean rename with the base-mod `troop-bay` template + the trooper/dropship designs updated in the same slice** (and a `[JsonConverter]`/id-alias if any save must survive). `GroundTransport` and the invasion chain (B5-3) read the generalized component. Do the promotion FIRST (C1), so nothing builds a second bay type.

### The Strike Craft — the shared ship designer (exists)
A strike craft is a small ship design (decision 1 — real built entities): **Fighter** (tiny hull + light guns + high thrust → high evasion, anti-ship — the dodge corner) or **Bomber** (bigger small-hull + an anti-capital payload + less thrust → less evasion, wants a fighter screen). Built through industry like any ship, **embarked into the carrier's `StrikeCraft` bay** as a wing-pool member.

### The Escort (the counter — also exists)
A **flak / point-defense escort** (`PointDefenseAtb`, flak profiles) — the flak corner of the triangle. Not new; fielding carriers just makes the enemy *want* it.

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
1. **Stand off.** The carrier's **own doctrine** (Carrier/Standoff role) holds it at range; it contributes little/no direct fire itself. Its doctrine supersedes the parent fleet's (decision 2).
2. **Launch a sub-fleet.** It moves up to **Cycle Rate** of craft out of the hangar **wing pool** and puts them into the battle **as a sub-fleet of real strike-craft entities** (decision 1). That sub-fleet carries **its own doctrine** (e.g. aggressive strike), which — like the carrier's — **supersedes the parent fleet's** (decision 2, the existing sub-fleet-overrides-parent model). *Only launched craft can be lost, and the carrier holds range — that's the whole "project force you don't expose" bet.*
3. **The wing fights via the normal fleet auto-resolve.** Because launched craft are just ships, they resolve on the exact same salvo math as any sub-fleet — high evasion (the dodge corner), light anti-ship guns. **Flak/PD on the enemy floors that evasion** (saturation) and kills craft fast; a gun-line barely scratches them. This is *cheap* despite being real entities because the auto-resolve **already buckets fire by weapon class** (`CombatKernel`) — 40 fighters cost about what 40 of anything cost, and the "1000-ship battle is cheap" guarantee holds.
4. **Attrition** = real craft die and leave the sub-fleet (entity deaths, the normal way).
5. **Recover & rearm.** Surviving craft return to the hangar at **Cycle Rate**, back into the wing pool, rearm, relaunch — the carrier's sustain (a fresh wave next pulse).
6. **Grave rungs:** if the **carrier dies**, its aloft sub-fleet is **stranded** (no deck to recover to) and bleeds out; if the **deck (bay) is suppressed** (a bay hit — the inherited `GroundBayAtb` grave rung), launch/recovery stop even though the hull lives.

The wing being a real sub-fleet (not a scalar) is what the developer chose over a pure aggregate — and it costs nothing extra because the fleet resolve was *already* aggregate under the hood. The *feel* (a wave melting under flak, a carrier caught and gutted, doctrine set per-wing) falls out of systems that already exist: the sub-fleet doctrine, evasion, and the flak/PD counter.

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

## 9. Decisions — LOCKED (developer, 2026-07-14)

1. **Strike craft = SHIPS, and REAL BUILT ENTITIES.** Not expendable ordnance. Built through industry like a ship (and like a ground unit is built), embarked into the carrier as a wing pool. *(The ordnance/missile-boat alt is rejected.)*
2. **Launched craft persist as ENTITIES and fight as a SUB-FLEET** — *not* an aggregate scalar. The wing pool is the hangar inventory; launching puts real craft into the battle as a sub-fleet. Performance is fine because the fleet resolve already buckets fire by class (§6). **The carrier's doctrine AND the launched sub-fleet's doctrine each SUPERSEDE the parent fleet's** (the existing sub-fleet-overrides-parent model).
3. **Add the Carrier/Standoff doctrine role** — a `CombatDoctrineBlueprint` posture (Rear-Guard/Artillery family): hold range, launch, don't close. Data-only.
4. **ONE generalized bay component** — the flight deck IS the troop bay IS the passenger berth, dialed by Bay Class (§5). Promote `GroundBayAtb → BayAtb`; do not build a separate hangar component.

---

## 10. Proposed build sequence (each a gauged slice, CI-verified, one at a time)

- **C1 — GENERALIZE the bay (the keystone; do FIRST).** Promote `GroundBayAtb → BayAtb` + `GroundCarryClass → BayClass` (add **StrikeCraft**, **Passenger**), add the **Cycle Rate** dial, and repoint the base-mod `troop-bay` template + the trooper/dropship designs + `GroundTransport` + the B5-3 invasion chain onto it. Gauge: the existing troop-transport tests (`GroundBayAtb`/`GroundTransport` gauges, `DevTest_UMF_CanBuildATroopTransport…`) stay green through the rename — **the troop bay is byte-identical, now a `BayAtb{Personnel}`.** *No carrier yet; this is the "don't build parallel systems" foundation.*
- **C2 — the flight-deck data + a carrier.** A base-mod `flight-deck` template = `BayAtb{ StrikeCraft }` with a fast Cycle Rate (JSON→atb, the gotcha-#10 sensor) + a fighter strike-craft ship design + a carrier hull that mounts the deck. Gauge: the deck binds its dials + `BayClass.StrikeCraft` from JSON and mounts on a ship (like `GroundUnitBaseModTests`). *Byte-identical to combat — nothing launches yet.*
- **C3 — the carry state + ops (engine, mirror `GroundTransport`).** `CarrierDB` (aboard **wing pool** + aloft **sub-fleet** handle) + `CarrierOps` (Embark / Launch-as-sub-fleet / Recover / Capacity). Launch spawns/activates the strike craft as a sub-fleet under the carrier (`TreeHierarchyDB`), tagged with the carrier/wing doctrine. Gauge: embark craft, launch up to Cycle Rate as a sub-fleet, recover survivors — pinned in a test. *Still byte-identical to the live battle (unwired).*
- **C4 — the resolver hook + doctrine.** `StepEngagement` gains the per-pulse launch/recover; the launched sub-fleet fights on the normal resolve; add the **Carrier/Standoff** doctrine posture (data) with sub-fleet-supersedes-parent. Gauge: a carrier + fighter wing loses to nothing on its own but, vs. a soft gun-line at range, **wins**; vs. a **flak escort**, the wing melts and the carrier loses — the triangle payoff through the real resolve. *The one real combat change; gate it for byte-identity if needed.*
- **C5 — bombers + AI.** A bomber strike-craft (heavy anti-capital payload, less evasive, wants a fighter screen) — the combined-arms layer; and the NPC brain fields a carrier group when its doctrine tilts that way. Gauge: bomber melts to flak alone, survives with a fighter screen; AI builds deck+craft.

Each slice: design the decision → build → **gauge in CI** → wait green → next. **C1 is pure refactor-to-general (byte-identical troop transport); the carrier rides in on C2–C4.** The counters (flak/PD) already exist, so the first *playable* carrier slice is a complete rock-paper-scissors, not a half-system.

---

## 11. One-paragraph summary (the point, restated)

A carrier is a **standoff force-projector**: it throws a swarm of evasive strike craft from beyond gun range, trading a soft, expensive hull and hard-countered-by-flak fragility for reach and dodge. It needs **no new combat engine** — launched craft are **real strike-craft entities fighting as a sub-fleet** in the one auto-resolve loop (cheap because that loop already buckets fire by class), and **their doctrine + the carrier's supersede the parent fleet's**. The **hangar is the troop bay, generalized**: one `BayAtb` component (Capacity · **Bay Class** [troops/vehicles/strike-craft/passengers] · **Cycle Rate**) that serves the trooper, the carrier, AND a passenger liner — `CONVENTIONS.md` §6, don't build parallel systems. The **strike craft are just small ship designs** (their cradle-to-grave already exists), and the **counter is already in the box** (flak/point-defense shred saturation-heavy evasive swarms). The genuinely new build is small: generalize the bay, add a `StrikeCraft` deploy handler (`CarrierDB`/`CarrierOps`, mirroring `GroundTransport`), and one resolver hook. The decision it adds — *project from range vs. close and brawl, and how much flak the enemy must buy to answer you* — stacks cleanly on detection, the weapon triangle, sub-fleet doctrine, and the mass budget.

---

## 12. What gets touched — the full blast radius of C1 (the bay generalization)

**Verified by a whole-repo sweep (2026-07-14).** C1 is `GroundBayAtb → BayAtb` + `GroundCarryClass → BayClass` (append `StrikeCraft`/`Passenger`/`Shuttle`, keeping `Personnel = 0`/`Vehicle = 1`). The carried-state blobs (`GroundTransportDB`) and the load/land statics (`GroundTransport`) **keep their names** — they're the Personnel/Vehicle handler; only their *reference* to the renamed atb/enum updates. Scope is bounded and tractable.

**The non-obvious find:** `GroundCarryClass` is **shared between the bay and the CHASSIS** — `GroundChassisAtb.CarryClass` declares what class a unit *is* (so the transport knows which bay hauls it). The base-mod frames encode it: `human-frame`/`swarm-frame` = `0` (Personnel), `vehicle-frame`/`walker-frame` = `1` (Vehicle). Keeping the two ordinals stable means the frames are byte-identical — but the rename **must** update `GroundChassisAtb` too, or the unit↔bay match breaks.

**Engine (8 files):**
| File | What changes |
|---|---|
| `GroundCombat/GroundBayAtb.cs` | **rename → `BayAtb`**; `enum GroundCarryClass → BayClass` (+ 3 appended values + `CycleRate` dial); `AtbName()` generalizes ("Troop Bay"/"Vehicle Bay"/"Flight Deck"/…) |
| `GroundCombat/GroundChassisAtb.cs` | `CarryClass` field type `GroundCarryClass → BayClass` (the coupling) |
| `GroundCombat/GroundTransport.cs` | reads `GroundBayAtb`/`CarryClass` for capacity + class-match → `BayAtb`/`BayClass` |
| `GroundCombat/GroundTransportDB.cs` | doc/ref to the atb name (kept as the Personnel/Vehicle carried-state) |
| `GroundCombat/GroundUnitAssembly.cs` | uses `GroundCarryClass` (chassis class in assembly) |
| `GroundCombat/LoadTroopsOrder.cs` · `LandTroopsOrder.cs` | read `GroundTransportDB` (unchanged) — only compile against the renamed enum if referenced |
| `Factions/ConquerResolver.cs` | `GroundBayAtb`/`BayCapacity` in `IsTroopTransport`/`FactionOwnsTransport` (the B5-3 invasion chain) → `BayAtb` |

**Data (JSON):** the `troop-bay` template's atb `AttributeType` FQN (`Pulsar4X.GroundCombat.GroundBayAtb` → the renamed class) in `installations.json`; the four `*-frame` templates keep their `CarryClass` `0`/`1` values (ordinal-stable — no change). No change to `componentDesigns.json`, the ship designs, or the faction files (they reference design ids, not the atb).

**Tests (7 files):** `GroundTransportTests`, `TroopOrderTests`, `GroundMobilityTests`, `GroundUnitPartsTests`, `GroundUnitPartsBaseModTests`, `GroundUnitAssemblyTests`, `DevTestScenarioTests` — update the symbol refs. **These are the byte-identity tripwire:** the troop-bay/transport gauges must stay green through the rename, proving the troop transport is unchanged (now a `BayAtb{Personnel}`).

**Client:** **CLEAN — zero references.** The component designer renders an atb via reflection off `AtbName()`/`AtbDescription()`, so no client code changes and the `build-client` CI job won't break.

**Save/load (landmine L3):** `GroundBayAtb`/`GroundCarryClass`/`GroundTransportDB` class names are embedded in saves via `TypeNameHandling`. The rename is a save-format change — **acceptable pre-release** (DevTest saves are throwaway); add a `[JsonConverter]`/type-alias only if a specific save must survive.

**Adjacent / future (NOT in C1):**
- **`cargo-Shuttlebay`** (`Storage/CargoTransferAtb`, "comes with shuttle!") is a *cargo/fuel-transfer* ability, a different atb — **a candidate to re-express as `BayAtb{Shuttle}` + a cargo-carrying shuttle craft** once the Shuttle class is built, but explicitly out of the C1 rename.
- **Namespace:** `BayAtb` stays in `Pulsar4X.GroundCombat` for C1 (minimal churn); moving it to a neutral home (e.g. `Pulsar4X.Ships`/`Pulsar4X.Storage`) is optional later polish (another save-format touch, so bundle it with L3 if ever done).

**Verdict:** bounded (8 engine files, 7 test files, 1 JSON FQN, no client), well-understood, and byte-identical for the troop transport. **The design is OFFICIAL — ready to build.**
