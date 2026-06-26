# Sensors Subsystem — Developer Reference

**What it does:** Detects other entities in space. Every ship and planet radiates and reflects electromagnetic energy across different wavelengths. Sensors pick up that energy and, if it's strong enough at the sensor's tuned wavelength, register a contact. Think of it like a ship's ESM/RADAR suite — you're listening for energy in specific frequency bands.

**Why it matters for ground combat:** Sensors determine whether you can see enemy fleets approaching, which drives when you get to respond to an incoming invasion. The ground-unit survey component in Phase 4 will also reuse the survey-points mechanic (not the EM detection mechanic).

---

## Files

| File | Role |
|------|------|
| `SensorProfileDB.cs` | DataBlob on every detectable entity. Holds `EmittedEMSpectra` (what it broadcasts) and `ReflectedEMSpectra` (what it bounces back from active sensors). Keyed by `EMWaveForm`. Also holds `ActivityMultiplier` (default 1.0): the **EMCON dial** that scales the entity's EMITTED signature at runtime ("how hot you run"); the future EMCON processor sets it from reactor load + thrust + firing. Read in `SensorTools.AttenuatedForDistance`; REFLECTED is **not** scaled (going dark doesn't shrink your hull). |
| `SensorAbilityDB.cs` | DataBlob on entities that can sense. Holds `CurrentContacts` and `OldContacts` (dict of entity ID → `SensorReturnValues`). |
| `SensorReceiverAtb.cs` | Component attribute defining a sensor. Peak wavelength, bandwidth, best/worst sensitivity in kW, resolution, scan time. Added to ship/station via `AddComponent()`. |
| `SensorSignatureAtb.cs` | Component attribute defining an entity's signature (what it emits). |
| `SensorReflectionProcessor.cs` | `IHotloopProcessor` running every **1 hour**. Updates the `ReflectedEMSpectra` on all entities with `SensorProfileDB`. |
| `SensorScan.cs` | `IInstanceProcessor`. Fires on a per-entity schedule (set by `sensorAtb.ScanTime`). Does the actual detection — calls `SensorTools.GetDetectedEntites()`, updates contacts. |
| `SensorTools.cs` | Static helper with all the math: `AttenuatedForDistance()`, `DetectonQuality()`, `GetDetectedEntites()`. This is where signal strength drops off with distance. |
| `SensorReturnValues.cs` | Struct returned from detection: `SignalStrength_kW` and `SignalQuality` (0–1, how much detail you can infer). |
| `SystemSensorContacts.cs` | Per-system contact list. `GetSensorContacts(factionId)` returns the faction's current known entities. |
| `SensorEntityFactory.cs` | Creates/updates the contact entity a faction sees (the "blip" on the map). |
| `EMWaveForm.cs` | Defines a band of EM spectrum: min wavelength, peak, max. |
| `SensorInfoDB.cs` | Per-contact info blob: last detection time, latest/highest detection quality. |
| `SensorPositionDB.cs` | Stores the last-known position of a detected entity (may lag reality if not recently scanned). |
| `Emcon/FleetEmconDB.cs` | DataBlob on a **fleet**: its active **EMCON posture** (`EmconPosture` enum — Full / Cruise / Silent), the run-hot/cruise/go-dark lever. Mirrors `Combat.FleetDoctrineDB` (a thin per-fleet "active choice" blob). The *choice* lives here (persistent, save/loaded, UI-visible); the per-ship effect is derived from it. |
| `Emcon/FleetEmcon.cs` | Helpers + setter for the posture lever. `MultiplierFor(posture)` (posture → emitted-signature scale: Full 1.0 / Cruise 0.5 / Silent 0.15), `PostureOf`/`MultiplierOf(fleet)` reads, and `SetPosture(fleet, posture)` — a **direct call** (like doctrine, so usable mid-combat) that pushes the posture's scale onto every member ship's `SensorProfileDB.SignatureBaseMultiplier` + `ActivityMultiplier` (recursing sub-fleet components). |
| `Emcon/EmconActivityProcessor.cs` | `IHotloopProcessor` (5 s, keyed to **ShipInfoDB**) that makes the signature respond to ACTIVITY: sets `ActivityMultiplier = SignatureBaseMultiplier × HeatFactor(burning, firing)`. **Thrust** read from `NewtonMoveDB.ManuverDeltaVLen` (no new field), **firing** from `GenericFiringWeaponsDB.ShotsFiredThisTick`. Pure statics `HeatFactor`/`ComputeActivityMultiplier`/`IsBurning`/`IsFiring` + tunables `ThrustHeat 4.0`/`WeaponHeat 1.0`. Reactor-load NOT folded in yet (the `Load` field is buggy — see EMCON build status). |

---

## How It Works

**Signal chain:**
1. Every entity has `SensorProfileDB` — it stores what frequencies it emits and reflects at what power (kW).
2. `SensorReflectionProcessor` updates reflected signatures every hour based on active illuminators nearby.
3. When a `SensorScan` fires for an entity with `SensorReceiverAtb`:
   - `SensorTools.GetDetectedEntites()` iterates every `SensorProfileDB` entity in the system.
   - For each target, `AttenuatedForDistance()` calculates how much signal survives the distance (inverse-square law).
   - `DetectonQuality()` compares the attenuated signal to the sensor's sensitivity at its tuned wavelength using a triangular overlap function.
   - If signal > threshold, a `SensorReturnValues` is produced: `SignalStrength_kW` (how strong) and `SignalQuality` (0.0–1.0 how much detail).
4. Contacts above threshold are added to `SensorAbilityDB.CurrentContacts` and `SystemSensorContacts`.

**Detection resolution:** Quality 0 = "something is there", Quality 1 = "full identification." Intermediate quality = partial information.

**Scan scheduling:** `SensorScan` reschedules itself: when it fires, it processes and then calls `ManagerSubpulses.AddEntityInterupt(now + scanTime, ...)`. So scan rate is determined by the sensor's `ScanTime` attribute.

---

## Key Formulas

Attenuation (from `SensorTools.AttenuationCalc`):
```
AttenuatedPower = sourcePower / (4π × distance²)
```

Detection quality: triangular overlap between sensor's waveform band and signal's waveform band. Signal must overlap the sensor's peak sensitivity region to score high quality.

---

## Pulsar Status vs Aurora

| Aurora concept | Pulsar | Status |
|----------------|--------|--------|
| Thermal signature (heat radiation) | `EmittedEMSpectra` at IR wavelengths | ✅ functional |
| EM signature (radar/active) | `ReflectedEMSpectra` + `SensorReflectionProcessor` | ✅ functional |
| Active vs passive sensors | `SensorReceiverAtb` tuned wavelength determines if it picks up emission or reflection | ✅ functional |
| Detection range scales with signature | `AttenuatedForDistance()` | ✅ functional |
| Contact quality (partial vs full ID) | `SignalQuality` 0–1 on `SensorReturnValues` | ✅ functional |
| EMCON (reduce your own signature) | `FleetEmconDB` posture (Full/Cruise/Silent) → ship `ActivityMultiplier` scales `EmittedEMSpectra` in the detection math | ✅ lever built (posture); runtime-activity inputs (reactor/thrust/firing) pending — see "EMCON build status" |
| Electronic warfare (jamming) | Not present | ❌ missing |

**Verdict: sensors are among the most complete subsystems in Pulsar.** The EM-spectrum physics approach is arguably more rigorous than Aurora's simpler thermal/EM bands. The main gaps are EMCON controls and EW jamming, neither of which are on the ground-combat critical path.

---

## EMCON build status (detection slice 3 — the dark/loud lever)

EMCON is being built as gameplay (a posture lever), **not** the EM-spectrum physics — the waveform math stays under the hood as the gauge needle. Full design + connected-systems ledger: `docs/DETECTION-DESIGN.md` §3/§3a/§6. Built in small gauged wires (each its own CI-green slice):

| Wire | What | Status | Gauge |
|------|------|--------|-------|
| **3a** | `SensorProfileDB.ActivityMultiplier` (default 1.0, the FINAL scale) read in the detection math (`SensorTools.AttenuatedForDistance` + `…List`), scaling **EMITTED** only (reflected/radar-return untouched). | ✅ built | `SensorEmconTests.ActivityMultiplier_*` |
| **3b** | `FleetEmconDB` posture (Full/Cruise/Silent) + `FleetEmcon.SetPosture` — the **player lever**: pushes the posture's scale onto every member ship's `SignatureBaseMultiplier` (and `ActivityMultiplier` for instant feedback). | ✅ built | `SensorEmconTests.SetPosture_*`, `FleetPosture_FlowsIntoTheRealDetectionMath` |
| **3c** | `EmconActivityProcessor` (hotloop, 5 s, keyed to **ShipInfoDB**) sets `ActivityMultiplier = SignatureBaseMultiplier × HeatFactor(burning, firing)`. **Thrust** (the dominant signal) read from `NewtonMoveDB.ManuverDeltaVLen > 0` (no new field — the burn state is already there); **firing** from `GenericFiringWeaponsDB.ShotsFiredThisTick`. Emergent: a lit drive plume betrays you even on Silent (you can't burn quietly). | ✅ built | `SensorEmconTests.HeatFactor_*`, `ComputeActivityMultiplier_*`, `Processor_OnIdleFleet_*` (heat math + composition CI-gauged; the real burning/firing reads are **live**-verified) |
| **3f** | active-ping self-exposure (an active sensor sweep adds to your own emitted spectra). | ⏳ next | — |
| **reactor-load** | folding "how hard the reactor runs" into the heat factor. The blocking `EnergyGenAbilityDB.Load` bug is **FIXED** (slice D — `EnergyGenProcessor.CalcLoad` = `Demand/TotalOutputMax` clamped 0-1), so this is now unblocked; left unwired as a marginal signal vs thrust. | ⏳ unblocked, deferred | — |
| **UI** | posture order in the Fleet window; contacts as blips; the sensor/EMCON **component** in the designer. | ⏳ client (slice 6) | local build (CI can't see the SDL client) |

**The model.** Posture sets a BASELINE loudness (`SignatureBaseMultiplier`); the activity processor multiplies it by a HEAT FACTOR (1.0 cold; +`ThrustHeat` while burning; +`WeaponHeat` while firing) into the final `ActivityMultiplier` the detection math reads. So posture × activity stack: a Silent ship that lights its drive (0.15 × ~5 ≈ 0.75) is far louder than one coasting (0.15) — the heat-asymmetry truth that you can't hide a hot plume — yet still quieter than the same burn at Full.

**Tunables (gameplay feel, like Combat's `SalvoDamageScale`):** in `FleetEmcon` — `FullMultiplier 1.0` / `CruiseMultiplier 0.5` / `SilentMultiplier 0.15` (Silent ≠ 0: never perfectly invisible). In `EmconActivityProcessor` — `ThrustHeat 4.0` (≈5× louder burning ⇒ ~2.2× detection range; the KEY knob) / `WeaponHeat 1.0`. A **switch cooldown** (reactor can't flash-cool) is a flagged candidate follow-up.

**Layering:** EMCON lives in `Sensors/Emcon/`, reads Fleets/Ships/Movement/Weapons to drive the dial it owns (`SensorProfileDB.ActivityMultiplier`), and is keyed to **ShipInfoDB** (one hotloop per DataBlob type — SensorProfileDB is taken by `SensorReflectionProcessor`, the same constraint that makes the battle trigger key to StarInfoDB). It does **not** depend on the combat resolver — clean direction is Combat → Sensors, never Sensors → Combat.

## Grave rung — a destroyed sensor blinds you (detection × damage)

The cradle-to-grave loss rung: shoot a ship's sensor receivers off and it stops detecting. **How it's wired:**
- `SensorTools.SetInstances(entity)` rebuilds the ship's receiver cache (`SensorAbilityDB.InstanceStates`/`InstanceAtributes`) from its CURRENT components — and now **clears the cache when no receivers remain** (previously it only rebuilt when receivers were present, so a fully-disarmed ship kept a phantom sensor).
- It's hooked to **`ReCalcProcessor.TypeProcessorMap[typeof(SensorAbilityDB)]`** (`Engine/Processors/RecalcProcessor.cs`), so every ability recalc rebuilds it. The **damage system calls `ReCalcProcessor.ReCalcAbilities(entity)` after destroying a component** (`DamageProcessor`), so losing your sensors empties the cache.
- The scan loop in `SensorScan` iterates `InstanceStates`, and its **reschedule is inside that loop** — so an empty cache means the ship neither scans nor re-schedules: it goes dark.
- **v1 limit (flagged):** the ship stops detecting NEW/refreshed contacts, but contacts it already put in the faction track table **persist until they age out** (no contact-expiry pass yet) — so a faction doesn't instantly forget. Contact aging is a separate follow-up.

Gauge: `SensorDetectionTests.DestroyingSensor_BlindsTheShip_GraveRung` — a watcher detects (receivers > 0, contacts > 0); remove its `SensorReceiverAtb` components + `ReCalcAbilities` (the damage-path tail); assert the receiver cache is empty and the next scan detects nothing.

## Live diagnostics — is the scan even firing?

`SensorScan.ScanCount` is a `public static long`, Interlocked-incremented at the top of `ProcessEntity` — a pure liveness counter (diagnostic only, no game effect). The client's `SessionLog` heartbeat logs it as part of the `[ENGINE] sensor scans N (+delta)` line, so a remote review can tell **"detecting nothing because nothing's in range" apart from "the scan never fired"** — the latter is a real risk, since the scan is only auto-scheduled by `Game.PostNewGameInitialization` (the test harness skips it). If the count doesn't climb while ships are present, the detection engine is dead, not quiet. Sibling counter on the combat side: `CombatEngagement.TickCount` (see `Combat/CLAUDE.md`).

## Phase 4 Relevance

Ground forces have a sensor component (`SensorSignatureAtb`) that gives them an EM profile when in space transport. On the ground, sensor ranges become terrain-line-of-sight problems — a different mechanic from the space EM system. Do not reuse `SensorScan` for ground unit spotting; it's the wrong tool.
