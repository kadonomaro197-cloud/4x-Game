# Hazard Discovery → Resistance → Research — Foundation Design

**Status:** foundation analysis (no code yet — settle the load-bearing decisions FIRST so we don't backpedal).
**Read with:** `GameEngine/Hazards/CLAUDE.md` (the spine that's built), `docs/DETECTION-DESIGN.md`, `docs/INFORMATION-DELTA-DESIGN.md`, the Prime Directive + Cradle-to-Grave in the root `CLAUDE.md`.

---

## The vision (two scenarios, one loop)

The developer described two things that are really **one vertical loop**:

> A ship with the right **sensors** is sent to a star/body. Its sensors **characterise** the hazard — "this star is throwing off *[a named kind of radiation]*." That intel goes **up the chain** (the player/AI is **notified** and **decides**). Knowing the hazard tells you **what armour/ability you need to survive there**. You **research** the counter — and over time a **research station at the site, gaining experience**, feeds **empire-wide research points** that **unlock a whole line of armour resistances** to *that specific* radiation, which **pays dividends late-game**. This works for **stars, planets, asteroids, comets — everything** (Stellaris-style discovery → research).

The chain, cradle to grave:

> **mineral → material → (base armour + environmental additive) ← unlocked by → research line ← fed by → a research station at the site + experience ← keyed to → a hazard CLASS ← characterised by → sensors ← which surveyed → the hazard a body/star emits → player/AI NOTIFIED → decides → builds → deploys → survives → late-game payoff.**

---

## What EXISTS vs MISSING (grounded, file:line)

### The hazard spine — BUILT (`GameEngine/Hazards/`)
Typed `HazardEffect` list, generic `HazardResistanceAtb` counter, JSON authoring, the six-point buildable counter, the grave rung. This is the floor everything else stands on.

### Sensors / survey / characterisation — INFRASTRUCTURE present, DISCOVERY absent
- ✅ Stars/bodies hold what they emit: `SensorProfileDB.EmittedEMSpectra` (`Sensors/SensorEmitter/SensorProfileDB.cs:78`) — wavelength + magnitude. **The raw data for "what does this star emit" already exists.**
- ✅ Survey mechanic works: `GeoSurveyableDB` + `GeoSurveyProcessor` (progress → completion → event).
- ❌ **Survey reveals MINERALS ONLY** (`GeoSurveyProcessor.cs:40-45` → `MineralsDB.GrantFactionPartialAccess`). It never reveals body/star properties or hazards.
- ❌ **No "characterise emission/hazard into faction knowledge"** — emissions are used only for detection-range math, never captured as "this faction has learned this star emits X."
- ❌ **`ServeyAnomalyAction` is a stub** (`Fleets/ServeyAnomalyAction.cs` — both methods `throw NotImplementedException`).
- ⚠️ **Detection quality is degenerate** (`SensorTools.cs:173` — feeds a 0–100 value to a 0–1 `PercentValue`, overflows). Any *quality-gated* reveal is blocked until this is fixed.

### Faction knowledge — only minerals/systems, no general "discovery" store
- ✅ `FactionInfoDB` stores `KnownSystems`, `KnownJumpPoints`, `SensorContacts`, `EventLog`.
- ✅ `Masked<T>` (`Engine/DataStructures/Masked.cs`) gates data per-faction (Full/Partial/None) — **used today only for minerals.** This is the precedent for "only factions who surveyed know."
- ❌ **No store for "known hazards / known star emissions / known body properties / known ruins."** `RadiationLevel` is copied in full on first detection (`SystemBodyInfoDB.cs:169`, never gated).

### Notification / "update system" — the PIPE works, nothing pushes discovery into it
- ✅ `EventManager` pub/sub + `FactionEventLog` (collects faction-relevant events, can auto-pause) + client `GameLogWindow`. Works end to end.
- ⚠️ Discovery event TYPES exist but are **never fired**: `SystemSurveyed`, `AnomalyDiscovered`, `RuinsLocated` (`Engine/Events/EventTypes.cs`). No hazard events at all.

### Research / tech — strong for labs, no site/experience research
- ✅ Empire-wide research pool, multi-level tech tree, **one tech can unlock a LINE of items** (`FactionDataStore.cs:195-199` iterates `Unlocks[level]`), category bonuses, `TechData('tech-…')` in design formulas (`ChainedExpression.cs:485`).
- ✅ **Tech can unlock ARMOUR** — `FactionDataStore.Unlock` moves `LockedArmor → Armor` (`:103-109`). So "research unlocks a line of armour resistances" is **already on rails.**
- ❌ **No site/experience-based research.** Research is colony-lab only. `ResearcherDB.LocationId` exists **but is never read.** No experience→points; scientist bonuses are static; `CommanderDB.Experience` is unused.

### Armour — single material, NOT a component, material ignored by auto-resolve
- ✅/❌ Armour is a **single `(ArmorBlueprint, thickness)` tuple** on the ship design (`ShipDesign.cs:50`), a **special hull property, NOT a component** — so it bypasses the component design/build chain.
- ✅ Armour material → `DamageResistBlueprint.WavelengthAbsorption[5]` drives the **per-pixel damage sim** (`DamageTools.DealDamageEnergyBeamSim`). **This is the mechanism hazard damage already uses.** Materials DO differ across bands (a tough alloy ~0.15, a poor one ~0.85).
- ❌ **Auto-resolve toughness counts thickness ONLY and ignores material** (`ShipCombatValueDB.cs:223`). So material matters in the damage sim/vs-hazards, **but not in fleet auto-resolve** — the "specialised armour pays dividends" promise breaks in the auto-resolve path unless wired.
- ❌ No base+additive / layered model; no additive/coating concept.

---

## The Prime-Directive connection map (why these are ONE system)

```
sensors ──characterise──▶ hazard CLASS ◀──keyed to── research line ──unlocks──▶ armour additive
   │                          ▲    │                      ▲                          │
 survey                       │  discovery store          │                       resists
   │                      damage signature          research station                │
   ▼                          │  (Masked<T>)          at the site + xp              ▼
 player/AI NOTIFIED ◀──events──┘        └──gates the DECISION──▶ build ──▶ deploy ──▶ survive/lose
```

Everything hinges on one shared thing in the middle: a **hazard CLASS / damage signature** that sensors name, the discovery store records, research is keyed to, and armour resists. Today that shared identity **does not exist as one thing** — it's split across two half-vocabularies (see Decision 1).

---

## THE LOAD-BEARING DECISIONS (settle these before any code)

### Decision 1 — ONE hazard-signature vocabulary (the keystone; everything else fails without it)
Today there are **two** partial "resistance" languages:
- the 6-value `HazardEffectType` enum (drives the `HazardResistanceAtb` counter, for the non-damage effects), and
- the 5 wavelength bands + `DamageResistBlueprint` (drives armour vs the damage effects).

Neither is a **named, data-defined, fine-grained "*this specific* stellar radiation"** you can survey, research a line against, and resist. The developer's vision ("a whole line of resistances to *that* radiation") **requires** that named identity.

**The move:** introduce a **data-defined Hazard Class** (a JSON blueprint with a stable id, e.g. `hazard-class-hard-uv`, `hazard-class-tidal-shear`, `hazard-class-em-storm`) that is the SINGLE thing threaded through all five systems: a hazard *emits* a class, a sensor *characterises* the class, the discovery store *records* the class, a research line is *keyed to* the class, and an armour additive / `HazardResistanceAtb` *resists* the class. Align it with the existing wavelength bands where it's a damage class (so armour material keeps working) **and** with the effect-kind enum where it's a non-damage class. **This is the one decision that, gotten wrong, forces a rewrite of the other four.**

### Decision 2 — a GENERAL faction-discovery layer (don't bolt on per-feature lists)
The vision needs "what has this faction discovered about this body/star/hazard." Build it ONCE as a general, `Masked<T>`-style discovery store keyed by entity/location, designed to hold **any** discovered property (hazard class present, star emission profile, radiation level, ruins) — not a one-off `KnownHazards` list that we rewrite when the next discoverable thing arrives.

### Decision 3 — armour = base (structural) + additive (environmental), gated by research
Restructure armour from one material to **base + additive layer(s)**, the additive's resistance keyed to a **Hazard Class** (Decision 1), and routed through the existing **`LockedArmor` → tech `Unlocks`** path so the research line actually unlocks it. Plus decide whether material finally counts in auto-resolve toughness (today it doesn't). This ripples through the damage sim, mass, combat value, designer UI, and save/load — bounded, but do it coherently in one pass.

### Decision 4 — site/experience research EXTENDS the lab system, not a parallel one
Model "a research station at the site + experience → empire-wide points that unlock the line" by **extending** the existing research: a location-aware research-points source (use the unused `ResearcherDB.LocationId`), a per-site experience accumulator that scales output, feeding a research category/field mapped to the site's Hazard Class, whose tech `Unlocks` the armour line. Reuse `GeoSurveyProcessor`'s accumulate-then-complete pattern. **Do not** fork a second research system.

### Decision 5 — "the update system": standardise a Discovery event family
The notification pipe works; the gap is that nothing fires discovery into it. Standardise a Discovery event family (fire the already-defined `AnomalyDiscovered`/`RuinsLocated` + add `HazardCharacterised`/`StarEmissionDiscovered`) carrying the Hazard Class, and surface it in `GameLogWindow` (and, if wanted, a decision prompt). **Clarify with the developer what "work on the update system" means** beyond the event feed — real-time alerts? a decisions queue? auto-pause-on-discovery (already supported via `_haltsOn`)?

---

## Resistance vehicles — passive armour, passive module, ACTIVE shield (developer note, this session)

**Reality check (verified):** *shields do NOT exist in the engine yet* — no `ShieldDB`/`ShieldAtb`/`ShieldProcessor`, no absorb-before-armour. The only traces are flavour text and, tellingly, the `defensive-systems` tech-category description: *"Research into protective armor and shields."* So the **intent** is in the data; the mechanic was never built. Likewise the *"three consumables"* (fuel / munitions / provisions) is only ~2/3 real: **fuel** (reactor energy, `RefuelAction`) and **ordnance** (missiles from cargo) exist; a **provisions / maintenance-supplies** consumable does **not** (`ResupplyAction` is a vague near-stub).

**Why this matters here:** a shield is the natural *environment-tuned* resistor — and it rides this foundation cleanly rather than fighting it. Three vehicles, ONE vocabulary (all keyed to the same `HazardClass`, Decision 1):

| Vehicle | Nature | Cost paid | Best at |
|---|---|---|---|
| **Armour material / additive** | passive, physical | hull **mass** (permanent) | always-on baseline; cheap to keep |
| **Hardening module** (built) | passive, electronic | a component slot | the non-damage effects (sensor jam) |
| **Shield** (NOT built) | **active, regenerating** | **power (continuous), + a supply later** | **environment-tuned, swappable; "patch the holes vs. fix nothing"** |

A shield is "easier/cheaper to tune for environments" than armour because you re-tune the emitter + feed it more reactor, instead of re-plating the hull — you pay in **power and commitment**, not mass. It rides the foundation: it resists a `HazardClass` like everything else (Decision 1), draws from the **existing** `EnergyStored` reactor pool, and slots into the damage path at **one clean intercept in `DamageProcessor.OnTakingDamage` BEFORE armour**. So it's the *active sibling* of the passive resistors — no new vocabulary, no foundation change.

### Shield design — SUPPLEMENTAL, active, signature-tuned (developer, this session)

A shield is **supplemental to armour, never a replacement.** Armour is the always-on physical baseline (mass); the shield is the **active, tunable add-on** you fit when you mean it. Design + mechanics:

- **It's a component (cradle-to-grave):** a `ShieldGeneratorAtb` — researched (Stellar/Energy + `defensive-systems`), built from materials, installed, **losable** (shot off → no shield). Design stats: **capacity** (max strength, J), **regen rate** (J/s), **tuning** (which DamageSignature(s) it's strong vs), **power draw**.
- **Costs a LOT of energy to RUN.** Continuous reactor draw just to keep it up, *plus* energy per point absorbed — it competes with weapons/sensors/warp for `EnergyStored` every tick. A shield up is a reactor committed. This is the "you gotta want it."
- **Costs a LOT of supplies when it BREAKS.** When capacity hits 0 the shield **collapses** (no protection while down); bringing it back from a collapse drains the **provisions/maintenance-supplies** consumable heavily (cheap to *hold up*, expensive to *recover*). *(That consumable doesn't exist yet — a flagged dependency; v1 may approximate with a one-off cost, but the intent is the shared supply pool, parking lot.)*
- **Absorbs BEFORE armour, up to a per-tick cap = "to a point".** Incoming damage (hazard OR weapon) hits the shield first; it absorbs up to a throughput **ceiling** set by tuning + capacity, draining charge. Anything **over the cap or past remaining charge bleeds through to armour.** This is *exactly* the existing weapon **saturation** lever: a high-rate-of-fire / flak barrage **overwhelms** a shield's per-tick cap and leaks, while a few big hits are eaten — so shields and the dodge/saturation triangle compose for free.
- **Bigger/better = bigger payoff, bigger cost.** Capacity, regen, tuning-breadth, and the per-tick cap all scale with **size + tech** (research climbs it). A *narrow* shield (one signature) is cheaper and stronger vs that one thing; a *broad* shield (many signatures) is the jack-of-all that pays more in power/mass. The "patch a hundred holes vs. fix nothing" payoff is real and scales.

**Decisions (defaults this session; revise freely):**
- **DESIGN NOW, BUILD LATER** — captured here so we don't backpedal; build it as its own phase after the survey→research loop, since it's a whole new system (component + regen/absorb processor + the damage intercept + power draw + combat-value wiring).
- **Cost = power to RUN + supplies to RECOVER from a break** (per the developer). The supplies half waits on the provisions consumable (parking lot).

### Keystone refinement — `HazardClass` IS a `DamageSignature`, shared by HAZARDS *and* WEAPONS

The developer's insight — *"a shield resistant to environmental kinetic damage is also resistant to ballistic weapons, to a point"* — collapses two things into one and makes the keystone (Decision 1) far stronger:

**A "kinetic" signature is the same whether it comes from a debris field (hazard) or a railgun (weapon). A "thermal/IR" signature is the same from a corona or a laser.** So the keystone class isn't a *hazard*-only label — it's a **DamageSignature** that *both* hazards and weapons EMIT, and that *both* armour (passive, by material wavelength) and shields (active, by tuning) RESIST.

This is the deepest "pays for itself": researching resistance to a signature to **survive an environment** *also* hardens you against **enemies who use that weapon type** — and vice-versa. One vocabulary, threaded through: weapons emit it, hazards emit it, sensors characterise it, the discovery store records it, research unlocks resistance to it, armour + shields resist it, the damage sim applies it. **Build Decision 1 as `DamageSignature`, not `HazardClass`** — wider, and it unifies combat with environment instead of running a parallel track. The existing weapon classes (Beam/Railgun/Flak) and wavelength bands map onto it directly.

---

## The holes / risks (what ruins this if ignored)

1. **Two resistance vocabularies (Decision 1)** — the #1 backpedal risk. Build the unified Hazard Class first.
2. **Detection-quality bug** (`SensorTools.cs:173`) — blocks quality-gated reveal; fix before gating discovery on quality (and re-check the two survey-reveal consumers it feeds).
3. **Auto-resolve ignores armour material** — "specialised shielding pays dividends" is invisible in fleet combat until material is wired into `ShipCombatValueDB`.
4. **Per-feature knowledge lists** — build the general discovery store (Decision 2) or rewrite later.
5. **Parallel research system** — extend, don't fork (Decision 4).
6. **Armour-model change is multi-file** — damage sim + mass + combat value + UI + save/load must move together; keep `additive == null` byte-identical to today (backward compat).
7. **NPC agency** — discovery/research/build must be drivable by the (currently thin) faction AI, or only the player gets the loop.
8. **Cradle-to-grave completeness** — the additive material still needs the mineral→refine→build rungs; the research station needs to be a buildable component; the loss rung (shot-off additive / destroyed station) must matter.

---

## Build order (foundations first — the anti-backpedal sequence)

**Phase 0 — foundations (no visible feature; everything stands on these):**
- 0a. Define the **Hazard Class** blueprint + thread it through the existing spine (hazards emit a class; `HazardResistanceAtb` resists a class; align with wavelength bands). *(Decision 1.)*
- 0b. Build the **general discovery store** (`Masked<T>`-style) on the faction. *(Decision 2.)*
- 0c. **Fix detection quality** (`SensorTools.cs:173`) + add a test. *(Prerequisite.)*

**Phase 1 — survey → discover → notify:** sensor/survey characterises a body/star's Hazard Class → writes the discovery store → fires a Discovery event → `GameLogWindow` shows it. *(Decision 5.)*

**Phase 2 — research the counter:** Hazard Class → research field/line; site-aware + experience research generation (extend `ResearcherDB.LocationId`); the research-station component; tech `Unlocks` the armour-resistance line. *(Decision 4.)*

**Phase 3 — armour + deploy:** base+additive armour model (data + damage sim + mass + combat value + UI + save/load), additive keyed to Hazard Class, gated by the Phase-2 unlock; deploy → survive → late-game payoff. *(Decision 3.)*

Each phase is gauged (engine tests, CI-covered) before the next. Phase 0 is the part to get RIGHT.

---

## Parking lot (deliberately deferred)
- **Shields** as the active, power-hungry, environment-tuned resistor (see "Resistance vehicles" above) — design captured, build it as its own phase after the loop is proven; it rides the `HazardClass` keystone + existing `EnergyStored`, intercepting in `DamageProcessor.OnTakingDamage` before armour.
- **Provisions / maintenance-supplies** consumable — the missing third of the fuel/munitions/provisions triad; build once, so repair + shields + logistics all draw on it (not a one-off shield supply).
- NPC strategic AI actually *using* the loop (avoid/survey/research/prepare) — the faction-AI layer is thin; a separate effort.
- Per-ship survivability readout (the INFORMATION-DELTA gauge) — strongly wanted, but a Phase-1/3 polish, not a foundation.
- Generalising discovery to ruins/anomalies/special projects (the `RuinsDB`/`AnomalyDiscovered` stubs) — the same discovery store + event family covers it; do after the hazard path proves the pattern.
- The `TidalShear`-style effect kinds that aren't a wavelength (need their own application site) — one enum value + one site each, per `GameEngine/Hazards/CLAUDE.md`.

---

## Decisions — LOCKED (developer, this session)
1. **Hazard Class granularity: COARSE (~5–8 classes)** to start — e.g. hard-radiation, thermal, EM-storm, tidal/gravitic, corrosive, kinetic-debris. Data lets us split into finer named classes later without code.
2. **Armour material DOES count in auto-resolve toughness** — wire material into `ShipCombatValueDB` (today thickness-only) so specialised armour pays off in fleet combat, not just the per-pixel damage sim.
3. **Research lives in a NEW "Stellar/Energy Science" field** — a dedicated tech category for studying stars/bodies/hazards (the Stellaris physics/energy feel), which the site-research loop feeds and whose techs `Unlocks` the armour-resistance lines.
4. **Update system = the Aurora model, which the engine already has the bones of** — discoveries fire typed events into the existing `FactionEventLog` → `GameLogWindow` feed, with auto-pause on important ones (`_haltsOn` already supports it). No new decisions-queue UI for v1; make the existing event feed carry the discovery family with the right severity/pause flags.
