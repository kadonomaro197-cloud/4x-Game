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
- NPC strategic AI actually *using* the loop (avoid/survey/research/prepare) — the faction-AI layer is thin; a separate effort.
- Per-ship survivability readout (the INFORMATION-DELTA gauge) — strongly wanted, but a Phase-1/3 polish, not a foundation.
- Generalising discovery to ruins/anomalies/special projects (the `RuinsDB`/`AnomalyDiscovered` stubs) — the same discovery store + event family covers it; do after the hazard path proves the pattern.
- The `TidalShear`-style effect kinds that aren't a wavelength (need their own application site) — one enum value + one site each, per `GameEngine/Hazards/CLAUDE.md`.

---

## Open questions for the developer (answer before Phase 0)
1. **Hazard Class granularity:** coarse (a handful: hard-radiation / thermal / EM-storm / tidal / corrosive) or fine (many named stellar/planetary radiations)? This sets how many research lines and armour additives exist. *(Recommend: start coarse, data lets you add fine later.)*
2. **Does armour material finally count in auto-resolve toughness**, or stay damage-sim-only? *(Recommend: yes, wire it — or the payoff is invisible in fleet combat.)*
3. **"Update system":** is the event feed + auto-pause enough for v1, or do you want a dedicated decisions/alerts queue?
4. **Research field:** a NEW "energy/stellar" research category per the Stellaris feel, or reuse `tech-category-defensive-systems` / `-sensors`?
