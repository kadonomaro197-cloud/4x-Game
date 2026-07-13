# The Capability Build Plan — from dead dials to a brain that uses them

**As of 2026-07-13.** The single, ordered implementation plan that **consolidates `docs/economy/COMPONENT-DESIGNER-DIAL-LEDGER.md` (what's built) + `docs/ai/AI-CAPABILITY-CATALOG.md` (what the AI can choose)** into one step-by-step build, sliced as few ways as possible. Produced from a 7-agent deep code audit (build→wire→test, file:line verified). This is the doc we build from.

---

## The thesis — why these two docs are one problem

The north-star is an NPC brain "aware of all its options." A utility scorer can only pick an option that is **(a) a real buildable, (b) actually wired** (it moves a number a resolver reads), and **(c) readable** by the catalog. Those three are exactly our two docs:

- **The dial ledger** = job (b): the component designer has ~25 built doors but many **dead dials** (the field exists, no resolver reads it). A scorer that builds a dead-dial component makes the AI waste industry on a thing that does nothing.
- **The capability catalog** = jobs (a)+(c): four whole needs (**INFLUENCE, DIPLOMACY-as-buildable, ESPIONAGE-as-buildable, GOVERNANCE-policy**) were called out as having **no buildable at all**. **Reconciliation (2026-07-13): ESPIONAGE-as-buildable has since landed** — `Factions/IntelDirectorateAtb.cs` is a real buildable component (the espionage twin of `ResearchAcademyAtb`), with `IntelDirectorateProcessor` + `CovertActionCatalog` + `InformationLedgerDB` attached to factions in `FactionFactory` — shipped as `IntelDirectorateAtb`, not the plan's `IntelNetworkAtb` (see slice 4.6). The other three (INFLUENCE, DIPLOMACY-Embassy, GOVERNANCE-policy) still have no component attribute in `Factions/`. And nothing yet reads a *catalog* to know its options (see Track 5 reconciliation).

So the brain is the **capstone**: it sits on top of, and is only as honest as, the wires (b) and the new content (a). This plan builds the foundation, then the brain.

### The dependency spine (one picture)

```
  TRACK 0  Data hygiene ─────────────┐  (unblocks catalog honesty)
                                      ▼
  TRACK 1  Combat wires  ┐
  TRACK 2  Chassis/Econ  ├─ (parallel, independent subsystems) ──┐
  TRACK 3  Sensor/Power  ┘                                        ▼
  TRACK 4  New content (INFLUENCE ▸ Embassy ▸ Spy-net) ──────────► TRACK 5  Utility brain
                                                                   (catalog → needs → scorer)
```

TRACKS 1–4 can proceed in any order / in parallel (each is its own subsystem, each slice byte-identical-first). **TRACK 5's *scorer* (Phase C) must come last** — its A-code (catalog) and B (needs model) can land early (byte-identical, no consumer), but the moment the scorer *chooses*, the dials must be wired (T1–T3) and the content must exist (T4) or the AI perceives options it can't use / builds things that don't work.

---

## Ledger corrections the deep pass caught (fixed in this pass)

The dial ledger was stale in two rows (both now flipped in `docs/economy/COMPONENT-DESIGNER-DIAL-LEDGER.md`):

1. **Chassis ▸ Hull mass-cap already BITES.** The ledger said "1/4, enforcement OFF." In code every base-mod ship mounts a hull, `ShipDesign.EnforceMassBudget` is set **true** in the client (`PulsarMainWindow.cs:139`), and two enforcement gauges pass in CI. The cap is live; what's left is the *hardpoint + hull-HP extension*, not "make it bite."
2. **Power ▸ Generation reactor-heat→EMCON already BUILT.** The ledger said "hook flagged, unread." In code `EmconActivityProcessor.cs:115` reads reactor `Load` into the signature (behind `EnableReactorHeat`), gauged by `ReactorHeatTests`. Only the client flag-flip remains.

*(These are exactly the kind of "doc claims a state the code moved past" the ledger exists to kill — caught by re-verifying against source.)*

---

## The slice plan

Legend per slice: **BI** = byte-identical (additive/gated, no fixture moves) · **RB** = re-baselines existing gauges (a deliberate rebalance, needs sign-off) · **flag** = lands behind a default-off static. Each slice = one push, wait for **both** CI jobs green before stacking the next (root pre-flight rule #6).

### TRACK 0 — Data hygiene (do first; cheap; makes the catalog trustworthy)

| # | Slice | Build · Wire · Test | BI/RB |
|---|-------|---------------------|-------|
| 0.1 | **Data-bug pass** | Fix the `spaceport` UniqueID collision (`installations.json:664` vs `storage.json:108` — the loader *reflection-overwrites* into a load-order-dependent hybrid, not a clean drop) + the broken atb ref `installations.json:736` (`Pulsar4X.Datablobs.CargoTransferAtbDB` → `Pulsar4X.Storage.CargoTransferAtb`). **Harden `BaseModIntegrityTests`** with a duplicate-UniqueID assertion + an "every `AttributeType` resolves against the assembly" assertion. | RB (de-hybridizes a live template, activates a dead transfer atb — low-risk, observable) |

### TRACK 1 — Combat depth (highest-value game wires)

| # | Slice | Build · Wire · Test | BI/RB |
|---|-------|---------------------|-------|
| 1.1 ★ | **Ship flat-armour soak + penetration plumbing** (the headline) | Add `ShipCombatValueDB.ArmourRating` (from armour thickness); teach `BuildFireMix`/`AddScaledFire` (`CombatEngagement.cs:1141,1383`) to carry `Penetration`/`PerShotEnergy` (today hard-zeroed); add `FleetFlatArmourSoak` and insert after the nature-armour step at `CombatEngagement.cs:705`, calling the CI-green `CombatKernel.ArmourSoakBurst`. Gauge `ShipArmourPenetrationTests`. **Zero base-mod dials set ⇒ BI.** Lights the armour-vs-alpha matchup for Energy + Ballistic + (later) Guided in one wire. | BI (engine wire, zero-data) |
| 1.1-data | **Dial the weapons** | Set `Penetration`/`PerShotEnergy` on base-mod beam/railgun/plasma/flak + give ships `ArmourRating`. | RB (`CombatStressLab`/`WeaponTriangle*` re-tune, sign-off) |
| 1.2 | **Multi-target fire control** | `BeamFireControlAtbDB.TargetsTracked`; cap fire-split at `CombatEngagement.cs:680` by fleet-summed capacity. `ShipMultiTargetTests`. | BI (flag `EnableMultiTarget`) |
| 1.3 | **Missile warhead de-stub** | Replace the 4 `Missile*Stub` reads (`ShipCombatValueDB.cs:429`) with a real `OrdnanceProfile.From(OrdnanceDesign)` (warhead J / seeker / velocity / shaped→penetration). **Prereq: resolve `AssignedOrdnance`-null-at-build** (default loadout source). `ShipMissileWarheadTests`. | RB (missile ships re-tune; needs a `MissileWarheadScale` dial) |
| 1.4 | **Shield nature-tuning** | `ShieldAtb.SoakVs{Kinetic,Energy,Explosive,Exotic}` (mirror `ArmourHardeningAtb`); `FleetShieldSoakFraction` into `ApplyShield` (`:1356`). `ShipShieldNatureTests`. | BI (default = current table) |
| 1.5 | *(deferred)* **Per-ship shields** | Move shield pool per-bucket in `ApplyCasualties` — perturbs the casualty seam shared with the parked degraded-condition model. Own design pass. | RB, deferred |

### TRACK 2 — Chassis gates + economic pressure (§0b + the money loop)

| # | Slice | Build · Wire · Test | BI/RB |
|---|-------|---------------------|-------|
| 2.1 | **Hull hardpoint budget** | `ShipHullAtb.HardpointBudget` + count weapon mounts in `ShipDesign.Recalculate` (reuse the `OverMassBudget`/`EnforceMassBudget` gate at `:244`). `ShipHardpointBudgetTests`. | BI *iff* per-tier hardpoint counts calibrated ≥ stock ships (the byte-identity gauge proves it — new uncalibrated numbers, flag) |
| 2.2 | **Assembly-bay size gate** | Read the already-stored `ProductionLine.MaxVolume` (shipyard `Slip Size`) vs design mass in `ConstructStuff` (`IndustryTools.cs:~125`); type-check cast, no `IConstructableDesign` change. `AssemblyBaySizeGateTests`. | BI (single-arg bays = +∞; confirm default Slip Size ≥ heaviest ship) |
| 2.3 | **Ground transport `Size` + Walker/Swarm class** | Repoint the built `GroundTransport.CarrySizeOf` at chassis `Size` (fallback to type-table when 0); append `Walker`/`Swarm` to `GroundCarryClass`. Extend `GroundTransportTests`. | BI (Size 0 falls back; new enum inert until authored) |
| 2.4 | **Fleet/army upkeep CLOCK** | New `TransactionCategory.FleetUpkeep` + a ship cost blob + `FleetUpkeepProcessor` (clone `StationUpkeepProcessor`). `FleetUpkeepTests`. **A standalone economic-pressure system** — verify start funds survive it. | BI (enum add) → behavior when it bills (tune like station upkeep) |
| 2.5 | **Mothball/reserve discount** | A stored-flag discount + reactivation delay on `FleetUpkeepProcessor`. Extend `FleetUpkeepTests`. Unblocks the Logistical▸Storage mothball dial. | BI until a ship is mothballed. Prereq 2.4 |
| 2.6 | **Society levers (food + jobs)** | Add a `foodstuffs` refined good + read it for `foodSupply` (`SustenanceProcessor.cs:56`); add `EmploymentAtbDB.Jobs` to work-building templates. Keep demand/job *coefficients* behind the `SetDemand` DevTools switch. `SustenanceTests`/`EmploymentTests`. **Landmines:** Earth has no reactor (a naive power demand browns it out); employment denominator is pop×0.5 (tiny job counts read as total unemployment). | BI while coefficients 0 (the switch is the behavior flip) |
| 2.7 | *(lower priority)* **Hull structural HP** | `ShipHullAtb.BaseHP` → `ShipCombatValueDB.Toughness`. Stage **4a: all tiers = 0 (BI machinery)**, then **4b: dial HP + re-baseline every combat fixture** (the one combat re-baseline in this track). | 4a BI / 4b RB, sign-off |
| 2.8 | *(lower priority)* **Located extraction** | Gas skimmer (`GasHarvestAtbDB` + a fuel good) + per-hex deposit source-of-truth (reconcile the body-pool vs hex double-count). | BI-until-built; per-hex needs the double-count reconciliation |

### TRACK 3 — Sensor / power / propulsion wires (cheap additive)

| # | Slice | Build · Wire · Test | BI/RB |
|---|-------|---------------------|-------|
| 3.1 | **Survey pass** | Kill the `100000` FIXME literal → `JPSurveyAbilityDB.Range_m` (`JPSurveyProcessor.cs:47`); add survey `Resolution` (depth); add the missing `JPSurveyTests`/`GravSurveyTests`. | BI (defaults = current literal/full-detail; 1b MineralsDB tiering — check first) |
| 3.2 | **Energy store rates** | `EnergyStoreAtb.MaxDischarge_KJps`/`ChargeRate_KJps`; clamp drain/fill in `EnergyGenProcessor` (`:24-27`). `EnergyStoreRateTests`. Battery-vs-capacitor decision. | BI (defaults +∞) |
| 3.3 | **Ground traction pass** | Amphibious (thread into `HexPathfinder.FindPath`), motive-power draw (into the built `GroundUnitAssembly` power gate), drive-mass→speed (into `GroundMobility.SpeedMultForUnit`). | BI (amphibious/motive); mass→speed BI *iff* `refMass` anchored to stock drive |
| 3.4 | **EW decoy + ECCM** | `DecoyAtb` spawns a lightweight signature entity the enemy `SensorScan` detects naturally; `EccmAtb` divides `JammingDivisorAgainst`. `ShipDecoyTests`. | BI (gated flags off / R=1.0) |
| 3.5 | **Solar test gap** | Add `SolarGenTests` (`AbsorbedPower` is a pure static). Test-only. | BI |
| — | *(deferred subsystems, NOT wires)* | Survey MODES (survey processor + 3 blobs + planet-view UI); Warp gate-network (H8, the dead `JPFactory.CreateConnection:124` stub); Exotic medium layer / teleport. | deferred |

### TRACK 4 — New player systems (the content GAPS — INFLUENCE first, per the developer)

**INFLUENCE** — "build a structure that converts their population instead of war." Full cradle-to-grave, routes to "take a planet" without a shot.

| # | Slice | Build · Wire · Test | BI/RB |
|---|-------|---------------------|-------|
| 4.1 ★ | **Influence buildable + gauge** | `InfluenceAtb` (a "Culture Center" `PlanetInstallation`, dials Strength/Reach) + `InfluencePressureDB` (per-colony accumulator, attach at `ColonyFactory.cs:87,208`) + `InfluenceTools` (pure math, health-scaled emission = grave rung for free) + `InfluenceProcessor` (own blob, L9; pull model: a Culture Center writes `ExternalBeliefPressure` onto in-reach targets) + the six-point base-mod Culture Center. `InfluenceProcessorTests`. **No legitimacy wiring yet** — just the gauge moves. | BI (new blob at 0 pressure) |
| 4.2 | **Pressure converts + insulation resists** | Add `LegitimacyInputs.ExternalBeliefPressure` (neutral 0) + a belief term in `ComputeLegitimacy` + the one-line read in `LegitimacyProcessor.cs:56`. Add `CulturalInsulationAtb` (the Defense▸Hardening cultural-insulation dial, built as a component) + wire the resist. `InfluenceLegitimacyTests` (rival legitimacy falls → collapse → rebellion begins; insulated colony holds). **The "convert their people" mechanic working end-to-end.** | BI (neutral sentinel until a structure exists) |
| 4.3 | **The payoff: defection + diplomacy cost** | Wire the *unbuilt* `RebellionDB.WindowExpired` (`:59`) to defect the province to `InfluencePressureDB.DominantInfluencerFactionId` (flip `FactionOwnerID` — the ground-capture primitive) + casus belli for the victim; standing `RelationDelta` souring on active hostile influence. End-to-end test: sustained influence flips a rival colony to you, victim gains a casus belli. **Take a planet with no shot fired.** | BI until influence is aimed at a rival |

**Faction capabilities as components** — the shared pattern + Diplomacy/Espionage.

| # | Slice | Build · Wire · Test | BI/RB |
|---|-------|---------------------|-------|
| 4.4 | **`FactionCapability` rollup** (shared prereq) | ~40-line static reader summing a `*Atb` across a faction's colonies (mirror `FactionRollup`). Neutral-when-absent. `FactionCapabilityTests`. **Unlocks Embassy + Spy-net together.** | BI (pure reader) |
| 4.5 | **Embassy (diplomacy buildable)** | `EmbassyAtb` (treaty-capacity budget, + optional relation-warm) + six-point JSON; gate `Treaties.Propose` on rolled-up capacity. `EmbassyCapacityTests`. **Cleanest of the three** (the treaty engine already exists and is called). | BI *iff* capacity-0 = legacy/unlimited (flag: the real "0 = no treaties" constraint re-baselines `DiplomacyTreatyTests`) |
| 4.6 | **Spy network (espionage buildable)** — **LANDED DIFFERENTLY (2026-07-13):** built as `IntelDirectorateAtb`, not the plan's `IntelNetworkAtb`. (a) `InformationLedgerDB` is now attached to factions in `FactionFactory` (`:385,:446`) — **done**. (b) The buildable shipped as `Factions/IntelDirectorateAtb.cs` (OpCapacity + CounterIntelRating → `IntelDirectorateDB`) with `IntelDirectorateProcessor` and `CovertActionCatalog.cs` present; the NPC covert-mirror is gated off by `NPCDecisionProcessor.EnableEspionageMirror = false`. Code exists and is wired (runtime unverified — CI can't run the client). The original build/wire/test spec below is retained for the record: (a) Attach `InformationLedgerDB` to factions in `FactionFactory` (all paths). (b) An espionage atb (network strength → `CovertRisk` agentSkill + agent slots) + six-point JSON + a covert-ops processor (spends slots to run `CovertActionCatalog` through `CovertRisk`, writes the ledger) behind a default-off flag. `CovertOperationsTests`. | BI (flag off + neutral blob) — landed |
| — | **Governance** — DO NOT build as a component | Model-mismatch: a regime (`GovernmentDB`) is an empire-wide *modulator*, not a thing you mount/accumulate/lose. Keep it a saved dial-set. Only *policy-slot capacity* is component-shaped, and it needs an edict system built first. **Deferred + documented, not forced onto the rails.** | deferred |

### TRACK 5 — The utility brain (capstone; consumes T1–T4)

> **⚠ Reconciliation — this Track's specific pipeline was BYPASSED by real development (2026-07-13).** The `CapabilityCatalog` / `NeedGauges` / `CapabilityScorer` classes below **do not exist in the code** (verified by grep — no file defines any of the three). The NPC brain shipped anyway, via a **different architecture**: `Factions/NPCDecisionProcessor.cs` runs a `Tick` that dispatches through an `IObjectiveResolver` **registry of 6 resolvers** — `AdvanceTechResolver`, `ConquerResolver`, `DefendResolver`, `ExpandResolver`, `GrowEconomyResolver`, `ConsolidateResolver` — rather than a catalog→needs→scorer utility scorer. The brain's real, up-to-date build-state is tracked in **`docs/ai/AI-BRAIN-BUILD-TRACKER.md` — treat that as the source of truth for this Track, not the table below.** Note the brain's action arms all ship **gated off by default**: `EnableOrderEmission`, `EnableDiplomaticProposals`, `EnableEspionageMirror`, `EnableIntelLedger` all default `false` in `NPCDecisionProcessor` (code exists and is wired; runtime unverified — CI can't run the client). The slices below describe the **originally-prescribed scorer path, which was superseded** — kept for design intent; do not read them as "still to build" without checking `docs/ai/AI-BRAIN-BUILD-TRACKER.md` first.

| # | Slice | Build · Wire · Test | BI/RB |
|---|-------|---------------------|-------|
| 5.1 | **`CapabilityCatalog` (A-code)** | Walk `ModDataStore.ComponentTemplates`; for each, resolve each property's `AttributeType` string via `Type.GetType(...)` and file the template under a NEED via a hand-authored `atb→NEED` table (~40 entries = the catalog doc in code). Cache on `Game.CapabilityCatalog` (per-game, not static). Defensive (tolerates the broken ref / collision). `CapabilityCatalogTests` (laser→OFFENSE, lab→TECH, sensor→INTEL, mine→ECONOMY). **No consumer yet.** | BI (pure read) |
| 5.2 | **Needs model (B) — existing gauges** | `NeedGauges.Assess(FactionState)` → a gap per NEED, wiring gauges that already exist: DEFENSE (`ThreatAssessment`/`FactionRollup.MilitaryStrength`), ECONOMY (`StalledJobs`/`Balance`), GROWTH (`MeanMorale`). `NeedGaugesTests`. | BI (pure read on per-tick scratch) |
| 5.3 | **Needs model (B) — missing gauges** | Build the **INTEL-coverage** gauge (systems-with-a-picket / sensor installs — the single most-cited missing reader) + `FactionRollup.ResearchOutput` (TECH). Without these the INTEL/TECH axes have options but no gap. | BI (new readers) |
| 5.4 | **Scorer (C) — the equivalence conversion** | `CapabilityScorer.Best(state, catalog, gaps)` (generalizes `MilitaryTarget.BestEnemyTarget`'s value×reach to "which buildable"); convert `AdvanceTechResolver` to ask the catalog instead of its hardcoded lab-finder, behind `EnableUtilityScorer=false`. Single-atb equivalence ⇒ **byte-identical even flag-on** — proves the wiring before new behavior rides it. `CapabilityScorerTests` (equivalence tripwire). | BI (flag) |
| 5.5 ★ | **Scorer (C) — first NEW axis: INTEL** | An INTEL resolver driven by the scorer: biggest INTEL gap + a buildable sensor = build a sensor post. **This is the marquee "the brain is aware of its options" payoff** ("build a long-range sensor post to watch ship movements" falls out for free). Then DEFENSE, then ECONOMY — one CI-green slice each. | BI (flag), then real behavior flag-on |

**The honesty guardrail (why T5 is last):** the scorer must skip **dead-knob** options (the ledger's ⚫ dials — else the AI builds things that do nothing) and must leave the four content NEEDs unscorable until T4 builds them. Concretely, the `atb→NEED` table should only list atbs the ledger marks wired, and the missile stub (1.3) should be finished or missiles de-weighted before OFFENSE scoring is trusted.

---

## Recommended global order (fewest slices to the two headline payoffs)

The developer's two headline wants define the critical paths:

**Payoff A — "convert their people instead of war":** `4.1 → 4.2 → 4.3`. Three slices, all byte-identical-first, delivers a peaceful take-a-planet. **No dependency on anything else** — can start immediately.

**Payoff B — "the AI aware of all its options":** `0.1 → 5.1 → 5.2 → 5.3 → 5.4 → 5.5`. The catalog/needs/scorer, but honest only once the dials it scores are wired. So the *minimum* honest version = T0 (hygiene) + T5 + the specific wires the first scored axes need (INTEL is clean today; OFFENSE wants 1.1+1.3).

**Suggested interleave** (each its own CI-green slice; ★ = highest value):
1. **0.1** data hygiene (unblocks catalog trust)
2. **1.1 ★** ship armour-penetration wire (lights 3 combat doors, BI)
3. **4.1 ★ → 4.2 → 4.3** INFLUENCE (payoff A, independent, BI-first)
4. **4.4 → 4.5 → 4.6** faction-capabilities (Embassy + Spy-net on one shared reader)
5. **5.1 → 5.2 → 5.3** catalog + needs model (BI, no consumer — safe to land anytime)
6. **5.4 → 5.5 ★** the scorer + INTEL axis (payoff B — *after* the wires it scores exist)
7. Fill in T1 (1.2–1.4), T2 (2.1–2.6), T3 (3.1–3.5) as depth passes; the RB slices (1.1-data, 1.3, 2.7-4b) batched with developer sign-off.

**Total minimal count:** ~28 CI-gated slices to build *everything*, of which the two payoffs need ~9 (three for INFLUENCE, six for the brain), and all but the flagged RB slices land byte-identical.

---

## Maintenance

This plan supersedes the scattered "wire-plan" in the dial ledger and the "Build path" in the capability catalog — both now point here. When a slice lands, flip its dial-ledger row (code state) **and** this plan's row in the same commit. The catalog's `atb→NEED` table (slice 5.1) and this plan must stay in sync — a new buildable adds a row to both.
