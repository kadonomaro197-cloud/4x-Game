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
| `Emcon/FleetEmcon.cs` | Helpers + setter for the posture lever. `MultiplierFor(posture)` (posture → emitted-signature scale: Full 1.0 / Cruise 0.5 / Silent 0.15), `PostureOf`/`MultiplierOf(fleet)` reads, and `SetPosture(fleet, posture)` — a **direct call** (like doctrine, so usable mid-combat) that pushes the posture's scale onto every member ship's `SensorProfileDB.ActivityMultiplier` (recursing sub-fleet components). v1 PUSH model; the runtime-activity inputs (reactor/thrust/firing) are the next slices. |

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
| **3a** | `SensorProfileDB.ActivityMultiplier` (default 1.0) read in the detection math (`SensorTools.AttenuatedForDistance` + `…List`), scaling **EMITTED** only (reflected/radar-return untouched). | ✅ built | `SensorEmconTests.ActivityMultiplier_*` |
| **3b** | `FleetEmconDB` posture (Full/Cruise/Silent) + `FleetEmcon.SetPosture` — the **player lever**: pushes the posture's scale onto every member ship's `ActivityMultiplier`. | ✅ built | `SensorEmconTests.SetPosture_*`, `FleetPosture_FlowsIntoTheRealDetectionMath` |
| **3c–3f** | make the signature respond to **runtime activity**: a processor that sets `ActivityMultiplier = posture × reactor-load × thrust × firing`; reactor throttle; runtime burn/fire state; active-ping self-exposure. | ⏳ next | — |
| **UI** | posture order in the Fleet window; contacts as blips; the sensor/EMCON **component** in the designer. | ⏳ client (slice 6) | local build (CI can't see the SDL client) |

**Tunables (gameplay feel, like Combat's `SalvoDamageScale`)** in `FleetEmcon`: `FullMultiplier 1.0` / `CruiseMultiplier 0.5` / `SilentMultiplier 0.15`. Silent is deliberately **not** zero — you can never be perfectly invisible (hull still reflects an active ping; a cold ship still leaks some heat). A **switch cooldown** (reactor can't flash-cool, so committing to dark would be a real tactical commitment) is a flagged candidate follow-up, left out of v1 to keep the lever a single wire.

**Layering:** EMCON lives in `Sensors/Emcon/` and reads Fleets/Ships to push the dial it owns (`SensorProfileDB.ActivityMultiplier`). It does **not** depend on the combat resolver — clean direction is Combat → Sensors (combat reads contacts/first-strike), never Sensors → Combat.

## Phase 4 Relevance

Ground forces have a sensor component (`SensorSignatureAtb`) that gives them an EM profile when in space transport. On the ground, sensor ranges become terrain-line-of-sight problems — a different mechanic from the space EM system. Do not reuse `SensorScan` for ground unit spotting; it's the wrong tool.
