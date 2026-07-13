# Sensors Subsystem — Developer Reference

**What it does:** Detects other entities in space. Every ship and planet radiates and reflects electromagnetic energy across different wavelengths. Sensors pick up that energy and, if it's strong enough at the sensor's tuned wavelength, register a contact. Think of it like a ship's ESM/RADAR suite — you're listening for energy in specific frequency bands.

**Why it matters for ground combat:** Sensors determine whether you can see enemy fleets approaching, which drives when you get to respond to an incoming invasion. The ground-unit survey component in Phase 4 will also reuse the survey-points mechanic (not the EM detection mechanic).

---

## Files

| File | Role |
|------|------|
| `SensorEmitter/SensorProfileDB.cs` | DataBlob on every detectable entity. Holds `EmittedEMSpectra` (what it broadcasts) and `ReflectedEMSpectra` (what it bounces back from active sensors). Keyed by `EMWaveForm`. Also holds `ActivityMultiplier` (default 1.0): the **EMCON dial** that scales the entity's EMITTED signature at runtime ("how hot you run"); the future EMCON processor sets it from reactor load + thrust + firing. Read in `SensorTools.AttenuatedForDistance`; REFLECTED is **not** scaled (going dark doesn't shrink your hull). |
| `SensorRecever/SensorAbilityDB.cs` | DataBlob on entities that can sense. Holds `CurrentContacts` as a **`List<(Entity, SensorReturnValues)>`** (not a dict), plus the receiver caches `InstanceAtributes`/`InstanceStates`. The per-receiver `CurrentContacts`/`OldContacts` **dictionaries** (entity ID → `SensorReturnValues`) live on `SensorReceiverAbility` (the InstanceStates), **not** here. |
| `SensorRecever/SensorReceiverAtb.cs` | Component attribute defining a sensor. Peak wavelength, bandwidth, best/worst sensitivity in kW, resolution, scan time. Added to ship/station via `AddComponent()`. |
| `SensorEmitter/SensorSignatureAtb.cs` | Component attribute defining an entity's signature (what it emits). |
| `SensorEmitter/SensorReflectionProcessor.cs` | `IHotloopProcessor` running every **1 hour**. Updates the `ReflectedEMSpectra` on all entities with `SensorProfileDB` (calls `SensorProfileTools.UpdateReflectionProfile`). |
| `SensorEmitter/SensorProfileTools.cs` | Static reflection-profile worker: `SetProfileDB(entity)` builds an entity's `SensorProfileDB`, and `UpdateReflectionProfile(profile, atDateTime)` (called each pass by `SensorReflectionProcessor.ProcessEntity`) recomputes the reflected spectra. |
| `SensorRecever/SensorScan.cs` | `IInstanceProcessor`. Fires on a per-entity schedule (set by `sensorAtb.ScanTime`). Does the actual detection — calls `SensorTools.GetDetectedEntites()`, updates contacts. |
| `SensorRecever/SensorTools.cs` | Static helper with all the math: `AttenuatedForDistance()`, `DetectonQuality()`, `GetDetectedEntites()`, plus the reverse-solve range readouts (`RangeForSignal`, `DetectionRange_m`, `DetectionRangeAgainst`). This is where signal strength drops off with distance. |
| `SensorRecever/SensorReceiverAbility.cs` | The per-receiver InstanceState. Holds the `CurrentContacts`/`OldContacts` **dictionaries** (entity ID → `SensorReturnValues`) that `SensorScan` reads/writes. |
| `SensorReturnValues.cs` | *(flat, at `Sensors/` root)* Struct returned from detection: `SignalStrength_kW` and `SignalQuality` (0–1, how much detail you can infer). |
| `SystemSensorContacts.cs` | *(flat, at `Sensors/` root)* Per-system contact list. `GetSensorContacts(factionId)` returns the faction's current known entities. |
| `SensorContacts/SensorEntityFactory.cs` | Creates/updates the contact entity a faction sees (the "blip" on the map). |
| `EMWaveForm.cs` | *(flat, at `Sensors/` root)* Defines a band of EM spectrum: min wavelength, peak, max. |
| `SensorContacts/SensorInfoDB.cs` | Per-contact info blob: last detection time, latest/highest detection quality. |
| `SensorContacts/SensorPositionDB.cs` | Stores the last-known position of a detected entity (may lag reality if not recently scanned). |
| `Emcon/FleetEmconDB.cs` | DataBlob on a **fleet**: its active **EMCON posture** (`EmconPosture` enum — Full / Cruise / Silent), the run-hot/cruise/go-dark lever. Mirrors `Combat.FleetDoctrineDB` (a thin per-fleet "active choice" blob). The *choice* lives here (persistent, save/loaded, UI-visible); the per-ship effect is derived from it. |
| `Emcon/FleetEmcon.cs` | Helpers + setter for the posture lever. `MultiplierFor(posture)` (posture → emitted-signature scale: Full 1.0 / Cruise 0.5 / Silent 0.15), `PostureOf`/`MultiplierOf(fleet)` reads, and `SetPosture(fleet, posture)` — a **direct call** (like doctrine, so usable mid-combat) that pushes the posture's scale onto every member ship's `SensorProfileDB.SignatureBaseMultiplier` + `ActivityMultiplier` (recursing sub-fleet components). |
| `Emcon/EmconActivityProcessor.cs` | `IHotloopProcessor` (5 s, keyed to **ShipInfoDB**) that makes the signature respond to ACTIVITY: sets `ActivityMultiplier = SignatureBaseMultiplier × (HeatFactor(burning, firing) + ReactorHeat×ReactorLoad) × CloakAtb.CloakFactor × JammerAtb.SelfSignatureFactor` (a cloak damps it down; an active jammer's beacon pushes it up; a **loaded reactor** raises it — S5, ⚙4 Power). **Thrust** read from `NewtonMoveDB.ManuverDeltaVLen` (no new field), **firing** from `GenericFiringWeaponsDB.ShotsFiredThisTick`, **reactor load** from `EnergyGenAbilityDB.Load` via `ReactorLoad(ship)`. Pure statics `HeatFactor`/`ComputeActivityMultiplier`/`IsBurning`/`IsFiring`/`ReactorLoad` + tunables `ThrustHeat 4.0`/`WeaponHeat 1.0`/`ReactorHeat 1.0`. The reactor term is gated behind `EnableReactorHeat` (default off → byte-identical; client-on); gauge `ReactorHeatTests`. |
| `SensorEmitter/JammerAtb.cs` | **NEW (S4, 2026-07-11)** The EW ▸ BARRAGE JAMMER COMPONENT (`IComponentDesignAttribute`) — the OFFENSIVE twin of the cloak: it blinds the ENEMY. `SensitivityDegrade` (≥1), `Range_m`, `SelfSignatureBoost` (≥1, the beacon). Static `JammingDivisorAgainst(receiverPos, receiverFactionId, entities)` = product over HOSTILE, in-range, live jammers of `1 + (degrade−1)×health` (health-scaled grave rung; never blinds its own faction), consumed in `SensorTools.GetDetectedEntites` to DIVIDE the receiver's usable signal down (byte-identical at 1.0, exactly like the hazard degrade). Static `SelfSignatureFactor(ship)` = the strongest live jammer's boost, multiplied into `ActivityMultiplier` in `EmconActivityProcessor` (the loud-beacon catch). Gated `JammerAtb.EnableJamming` (default off → wholly inert → byte-identical; client-on). v1 = barrage only (targeted-jam + ECCM defer). Base-mod `jammer` (`electronics.json`) on the **Havoc Escort Jammer**. Gauge `ShipJammerTests`. |
| `SensorEmitter/CloakAtb.cs` | **NEW (S2, 2026-07-11)** The EW ▸ CLOAK / signature-damping COMPONENT (`IComponentDesignAttribute`, CONVENTIONS §6). Holds `SignatureMultiplier` (0..1, clamped [`MinSignatureFactor` 0.02, 1]); static `CloakFactor(entity)` = the **strongest** installed cloak (lowest factor), each health-scaled (`1 − (1 − SigMult)×HealthPercent`, so a shot-off cloak → 1.0 = the grave rung), else 1.0. `EmconActivityProcessor` multiplies the final `ActivityMultiplier` by it, so a cloak damps the emitted signature DOWN further than posture alone → detected at ≈√factor of the range. **No cloak → 1.0 → byte-identical** (every current ship). v1 damps the EMITTED signature only (an active radar ping still bounces off the hull). Base-mod `cloak-device` (`electronics.json`) on the **Wraith** stealth cruiser. Gauge `ShipCloakTests`. |

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
AttenuatedPower = sourcePower × DetectionSensitivityScale / (4π × distance²)
```

**`DetectionSensitivityScale` (1e6, added 2026-06-27) — the detection-range tuning dial.** The raw inverse-square
law was so harsh at realistic scales that a ship detected another ship at only **~292 km** (measured by
`DetectionTuningTests`). With fog of war on, that meant a fleet could sit AT a body and never see the hostiles
parked there, **and combat — which needs mutual detection — could never trigger** (the developer's "sat at Luna,
saw nothing, no battle" play-test, 2026-06-27). This is the long-standing in-code TODO ("the default sensor on
Earth doesn't even detect Uranus"). The scale multiplies the received signal **uniformly**, so detection RANGE
scales by its **square root**: 1e6 → ~1000× range → a ship sees a ship at **~0.29 Gm**. That covers same-body
combat + modest approach warning, while staying far below the ~60 Gm inner-system scale, so **fog of war is
preserved** (you do NOT see the whole system). Because it's uniform, every RELATIVE result (loud-seen-farther,
shrinks-on-Silent, the EMCON ladder) is unchanged — only the absolute reach moves. Applied in `AttenuationCalc` +
`AttenuationFactor` (the live scan) AND `RangeForSignal` (the readout), so the scan and the "how far can I see"
number stay in exact agreement. Gauges: `DetectionTuningTests` (ship-vs-ship clears the 0.1 Gm combat floor; reach
stays under the fog ceiling). Tunable like Combat's `SalvoDamageScale` — one number for the whole detection feel.

**Colony sensors see ACROSS the system — by design (decision 2026-06-28).** A follow-up to the scale tuning: detection
range is **linear in antenna size** (`threshold ∝ 1/antenna²`, range `∝ 1/√threshold ∝ antenna`), and the colony's
Passive Scanner has a **5000** antenna vs a ship's **5.5** — ~900× — so the colony detects a ship at **~230 Gm**, well
past Venus (~60 Gm): it sees the whole inner system. Because the 1e6 scale is ONE global knob and the two sensors are
locked ~800× apart by antenna size, you **cannot** rein the colony in without dropping the ship sensor back under the
combat-useful floor. The developer's call: **keep it.** A fixed ground megasensor is realistic system-wide early
warning — the homeworld always spots fleets entering the system, but the fog that matters for tactics (ship-vs-ship in
deep space) is still the tight ~0.3 Gm bubble. So a colony is intentionally NOT a "tactical bubble" sensor; ships are.
Gauge: `DetectionTuningTests.ColonyScanner_IsSystemWideEarlyWarning_ByDesign` (asserts the colony reach dwarfs a
warship's — that asymmetry IS the design). If a future build wants surprise approaches on the homeworld, that needs a
deliberate per-design sensor-horizon cap, not a scale change.

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
| Electronic warfare (jamming) | `JammerAtb` barrage jammer → divides a hostile receiver's usable signal in `GetDetectedEntites` (+ the loud-beacon self-cost); gated `EnableJamming` | ✅ barrage built (S4); targeted-jam / ECCM defer |

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
| **UI** | posture order in the Fleet window (✅ slice A); **detected foreign units shown as contact blips + undetected hidden** (✅ — `SensorContactIcon` + the `SystemMapRendering` fog guard, gated on `RequireDetectionToEngage`: the visual half of fog of war); the sensor/EMCON **component** in the designer (⏳). | ✅ blips/fog built | local build (CI can't see the SDL client) |

**The model.** Posture sets a BASELINE loudness (`SignatureBaseMultiplier`); the activity processor multiplies it by a HEAT FACTOR (1.0 cold; +`ThrustHeat` while burning; +`WeaponHeat` while firing) into the final `ActivityMultiplier` the detection math reads. So posture × activity stack: a Silent ship that lights its drive (0.15 × ~5 ≈ 0.75) is far louder than one coasting (0.15) — the heat-asymmetry truth that you can't hide a hot plume — yet still quieter than the same burn at Full.

**Tunables (gameplay feel, like Combat's `SalvoDamageScale`):** in `FleetEmcon` — `FullMultiplier 1.0` / `CruiseMultiplier 0.5` / `SilentMultiplier 0.15` (Silent ≠ 0: never perfectly invisible). In `EmconActivityProcessor` — `ThrustHeat 4.0` (≈5× louder burning ⇒ ~2.2× detection range; the KEY knob) / `WeaponHeat 1.0`. A **switch cooldown** (reactor can't flash-cool) is a flagged candidate follow-up.

**Layering:** EMCON lives in `Sensors/Emcon/`, reads Fleets/Ships/Movement/Weapons to drive the dial it owns (`SensorProfileDB.ActivityMultiplier`), and is keyed to **ShipInfoDB** (one hotloop per DataBlob type — SensorProfileDB is taken by `SensorReflectionProcessor`, the same constraint that makes the battle trigger key to StarInfoDB). It does **not** depend on the combat resolver — clean direction is Combat → Sensors, never Sensors → Combat.

## SensorContact — UI read accessors (added 2026-06-26)

The map's fog-of-war blips (`SensorContactIcon`, client side) need a contact's signal strength and whether it's a stale "memory" position, but those live in the **internal** fields `SensorInfoDB.LatestDetectionQuality` and `SensorPositionDB.GetDataFrom` — the client is a separate assembly and can't reach them. `SensorContact` (engine, same assembly) exposes them as public, computed, **never-serialized** accessors:
- `SensorContact.SignalStrength_kW` — latest return loudness (kW); the blip scales a touch by it.
- `SensorContact.PositionIsMemory` — TRUE once the real entity is gone and the contact coasts on its last-known position (the grave rung); the blip fades and the label reads "(last known)".

Pure pass-throughs (CI compiles them; no behavioural test needed). When new UI needs more of a contact's *known* info, expose it here — don't widen the internal fields' visibility.

## Detection RANGE accessors (2026-06-27) — the reverse-solve, so "how far can I see" is a number

The scan only ever asks "is this target's faded signal above threshold at its *current* distance?" (a yes/no, fresh every scan) — so no "how far can I see" number existed for the UI to draw. There's no `DetectionRange` field, and that's *correct*: range depends on how loud the **other** ship is (dark vs. hot), so it's a relationship, not a property. `SensorTools` now runs that same attenuation **backwards**:
- `RangeForSignal(source_kW, threshold_kW)` = `√(source / 4π·threshold)` — the exact inverse of `AttenuationCalc`. Inverts only the FIRST gate of `DetectonQuality` (a band is detectable when its attenuated magnitude > `BestSensitivity_kW`); ignores the waveform-overlap quality refinement (that shapes *how well* resolved, not *whether* seen).
- `DetectionRange_m(receiver, target)` — how far that receiver first picks up that target: loudest band wins; **emitted** scales by the target's `ActivityMultiplier` (run hot = seen farther), **reflected** does not (going dark doesn't shrink your hull).
- `TryGetBestReceiver(entity, out atb)` — the ship's most sensitive receiver (lowest `BestSensitivity_kW`, skipping `IsEnergyGen` solar arrays). Reads the same `SensorAbilityDB.InstanceAtributes` cache the scan uses, so a sensor-less / shot-blind ship returns false (the grave rung).
- `SelfDetectionRange_m(entity)` — "a ship like me, running as I am, I'd first see at range R." Uses the ship's **own** `SensorProfileDB` as a magic-constant-free reference target; reads live `ActivityMultiplier`.

**SEE vs BE-SEEN — two distinct numbers (the mislabel fix, 2026-06-27).** `SelfDetectionRange_m` reads the ship's
LIVE activity, so it **shrinks on Silent** — which is correct for *detectability* but WRONG when drawn as "sensor
reach" (going dark must not reduce how far YOU can see). The client ring was mislabeled this way. Now split cleanly,
both off the same `DetectionRange_m` with the new `activityOverride` param:
- `SensorReachRange_m(entity)` — **how far you can SEE.** Your receiver vs a reference target pinned to FULL
  activity (`activityOverride: 1.0`), so it does **not** move with your own EMCON — only a louder/quieter *target*
  moves it. The green map ring + the "Can See" column.
- `DetectabilityRange_m(entity)` — **how far you can BE SEEN.** Your live emitted signature (EMCON × thrust × fire)
  vs a receiver as good as your own; **shrinks on Silent, grows running hot** (= `SelfDetectionRange_m`, renamed for
  intent). The amber map ring + the "Seen At" column.
- `CurrentActivityMultiplier(entity)` — the live emitted-signature scale (1.0 = as-designed, <1 quiet, >1 hot) — the
  driver shown next to detectability so the player sees WHY it moved.
At Full activity reach == detectability (both at 1.0); they diverge the moment you change EMCON or burn.

Feeds the client (Fleet Combat tab "Can See"/"Seen At" columns + green/amber map rings + the activity readout).
Gauged by `Pulsar4X.Tests/RangeReadoutTests.cs` (round-trip; loudest-band/activity; self-ring shrinks on Silent;
**+ `SensorReach_vs_Detectability_UnderEmcon`** — reach unchanged on Silent, detectability shrinks). Survey +
framing: `docs/INFORMATION-DELTA-DESIGN.md`. The against-a-specific-target range is now built in the engine as
`SensorTools.DetectionRangeAgainst(detector, target)` (`SensorTools.cs`) — "how far can THIS ship pick up THAT
target," using the detector's best receiver and the target's actual (loud-vs-dark) signature. Whether the client
draws a ring from it against the selected enemy contact is the local-build UI step (CI can't see the SDL client).

## Detection-quality bug — FIXED (2026-06-28, `SensorTools.cs` `DetectonQuality`)

**The bug (was):** `SensorTools.DetectonQuality` built `SignalQuality` as `new PercentValue((float)(100 - distortion / signalWaveSpectraFreqMax))`. **`PercentValue(float)` expects 0..1** (it does `_percent = (byte)(value * 255)`), but this fed it a **0..100** value — so `~100 × 255 ≈ 25500` overflowed the byte and wrapped, making detection quality **degenerate** (effectively random, not "how well resolved"). Because quality gates **planet/star survey reveal** (`Galaxy/SystemBodyInfoDB.cs:154-160` and `Galaxy/StarInfoDB.cs:130`, thresholds 0.20 / 0.80), survey reveal was random too.

**The fix:** `quality = new PercentValue((float)Math.Clamp(1.0 - distortion / signalWaveSpectraFreqMax, 0.0, 1.0))` — a true 0..1 fraction. `distortion` (how far the signal's peak wavelength sits from the receiver's ideal band, nm) divided by the signal's max wavelength is a dimensionless mis-tune fraction, so a well-centred signal scores ~1 and an off-band one ~0; the clamp guards a mis-tune larger than the band. This is the **Phase-0 prerequisite** for the hazard discovery/research loop (a survey must report a *real* confidence before discovery can gate on it).

**Gauge:** `Pulsar4X.Tests/SensorQualityTests.cs` (CI) — a perfectly-tuned signal resolves at **1.0** (the regression guard: pre-fix it read ~0.74, a wrapped byte), quality is always in [0,1], and better alignment ⇒ higher quality.

**Survey consumers (the "look at the other end" check):** both `SystemBodyInfoDB.UpdateDatablob` and `StarInfoDB.Update` read `SignalQuality` as a 0..1 confidence (reveal body type > 0.20, tectonics/star detail > 0.80; `RndSigmoid` noise scaled by it). With the old garbage value these thresholds fired at random; the fix makes them fire on actual sensor↔signal alignment — strictly more correct. The *live* effect on a real survey (which bodies now read as Unknown vs identified) is the developer's local-build check; CI gauges the formula.

**Now unblocked (next slices):** progressive contact ID ("an *unknown* blip until you resolve it") and the discovery-confidence gate both depend on a real quality number, which now exists. The per-band quirk below is the next cleanup if needed.

**Known remaining quirk (out of scope for this fix):** when a signal has multiple wavebands, `quality` is **overwritten** each loop iteration (last detectable band wins) rather than taking the max. Most entities have one dominant band, and the time-aggregated `HighestDetectionQuality` smooths it, so this is left as a flagged follow-up — fixing it is a behaviour change (max-across-bands) that wants its own test, not a rider on the overflow fix.

## Grave rung — a destroyed sensor blinds you (detection × damage)

The cradle-to-grave loss rung: shoot a ship's sensor receivers off and it stops detecting. **How it's wired:**
- `SensorTools.SetInstances(entity)` rebuilds the ship's receiver cache (`SensorAbilityDB.InstanceStates`/`InstanceAtributes`) from its CURRENT components — and now **clears the cache when no receivers remain** (previously it only rebuilt when receivers were present, so a fully-disarmed ship kept a phantom sensor).
- It's hooked to **`ReCalcProcessor.TypeProcessorMap[typeof(SensorAbilityDB)]`** (`Engine/Processors/RecalcProcessor.cs`), so every ability recalc rebuilds it. The **damage system calls `ReCalcProcessor.ReCalcAbilities(entity)` after destroying a component** (`DamageProcessor`), so losing your sensors empties the cache.
- The scan loop in `SensorScan` iterates `InstanceStates`, and its **reschedule is inside that loop** — so an empty cache means the ship neither scans nor re-schedules: it goes dark.
- **v1 limit (flagged):** the ship stops detecting NEW/refreshed contacts, but contacts it already put in the faction track table **persist until they age out** (no contact-expiry pass yet) — so a faction doesn't instantly forget. Contact aging is a separate follow-up.

Gauge: `SensorDetectionTests.DestroyingSensor_BlindsTheShip_GraveRung` — a watcher detects (receivers > 0, contacts > 0); remove its `SensorReceiverAtb` components + `ReCalcAbilities` (the damage-path tail); assert the receiver cache is empty and the next scan detects nothing.

## The first-scan kick — a sensor installed MID-GAME now schedules its own first scan (2026-07-03)

`SensorScan` self-reschedules (each fire queues the next), but it needs a FIRST fire to bootstrap that loop. At New Game, `Game.PostNewGameInitialization` provides that kick — it fires the scan by hand on every sensor-bearing entity. But that runs **once, at game start**. A sensor installed **later** — a freshly built listening-outpost station, a DevTools-spawned ship, a repaired sensor — never passed through that path, so it sat **deaf forever**: `SensorReceiverAtb.OnComponentInstallation` only called `SensorTools.SetInstances` (rebuild the receiver cache), never scheduled a scan.

**Fix:** `OnComponentInstallation` now also schedules the first scan — `manager.ManagerSubpulses.AddEntityInterupt(StarSysDateTime + ScanTime, nameof(SensorScan), entity)` — future-dated off `StarSysDateTime` so it can never throw "interrupt in the past" during construction, and guarded on a null manager. From that first fire, `SensorScan.ProcessEntity` self-reschedules as before. It's idempotent with `PostNewGameInitialization` (an extra scheduled scan is harmless — the scan rebuilds contacts each fire, and `TimeQueue` tolerates a duplicate time). **Blast-radius note:** this also means the **test harness** now scans like the live game — `TestScenario.CreateWithColony` builds a colony whose Passive Scanner install now schedules a scan, so a feature test that only `AdvanceTime`s will see the colony detect + first-contact fire where before nothing scanned (the harness skips `PostNewGameInitialization`). More faithful to live, but a behaviour change to be aware of. Gauge: `StationFactoryTests.ListeningOutpostStation_DetectsHostileShip_ViaInstallScheduledScan` (installs a sensor on a live station, advances the clock with NO hand-fired scan, asserts the station's own `SensorAbilityDB` detects a hostile ship — which only works if the install-kick scheduled the scan).

## Live diagnostics — is the scan even firing?

`SensorScan.ScanCount` is a `public static long`, Interlocked-incremented at the top of `ProcessEntity` — a pure liveness counter (diagnostic only, no game effect). The client's `SessionLog` heartbeat logs it as part of the `[ENGINE] sensor scans N (+delta)` line, so a remote review can tell **"detecting nothing because nothing's in range" apart from "the scan never fired"** — the latter is a real risk, since the scan is only auto-scheduled by `Game.PostNewGameInitialization` (the test harness skips it) **and, as of 2026-07-03, by `SensorReceiverAtb.OnComponentInstallation` on a mid-game install — see "The first-scan kick" above**. If the count doesn't climb while ships are present, the detection engine is dead, not quiet. Sibling counter on the combat side: `CombatEngagement.TickCount` (see `Combat/CLAUDE.md`).

## First contact — a detection is the diplomacy front door (2026-07-01)

When `SensorScan` adds a **new** sensor contact for a foreign entity, it calls
`Pulsar4X.Factions.FirstContact.OnDetection(faction, detectableEntity, atDateTime)` (one line, in the new-contact
branch). That is where two factions first "meet": it records a mutual Neutral relationship row on both factions'
`DiplomacyDB` and fires a first-contact event. The `DiplomacyDB.HasMet` guard inside makes it fire once per faction
pair (the first foreign entity a faction ever detects is always a NEW contact, so the new-contact branch is the
correct, sufficient hook). It is defensive (never throws) and a no-op for neutral (planet/asteroid) or own-faction
targets — so the sensor hot loop is unaffected in the common case. Direction is Sensors → Factions (writes
`DiplomacyDB`), same as the scan already reaching `Game.Factions`. Full design: `docs/DIPLOMACY-DESIGN.md`; gauge
`DiplomacyFirstContactTests`. **v1 limit (flagged):** the hook is in the new-contact branch, so a save that already
holds a foreign contact from before this code (no diplomacy row) won't retro-register — harmless (IFF defaults to
hostile), and a contact-aging/backfill pass is the follow-up.

## Phase 4 Relevance

Ground forces have a sensor component (`SensorSignatureAtb`) that gives them an EM profile when in space transport. On the ground, sensor ranges become terrain-line-of-sight problems — a different mechanic from the space EM system. Do not reuse `SensorScan` for ground unit spotting; it's the wrong tool.
