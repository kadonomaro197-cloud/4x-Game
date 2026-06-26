# Detection Design — what exists, what we keep/cut/add, how it comes together

*Draft 2026-06-26 — the design pass BEFORE we build. Detection is M1 lever #1 (`docs/MVP.md`, `docs/REALISM-VS-GAMEPLAY-AUDIT.md`). This doc is for marking up: the Keep/Cut/Add calls in §3 and the open questions in §5 are proposals, not settled.*

---

## 0. The point first (what detection is FOR)

**Space is not an ocean — you cannot hide if you are HOT.** A ship at full burn is a beacon you can read from far off; a ship running cold is nearly invisible. Crucially, how far *you* see depends on the **target's** heat and **your** sensors — *not* on your own emissions — so a cold ship sees a hot ship LONG before the hot ship sees it. That asymmetry, not a symmetric "we both go quiet and both go blind," is the game. The decisions the physics forces:

- **Do I even know the enemy is there?** (fog of war)
- **How hot do I run?** Full burn / hot guns / active sensors gets me there fast and hits hard — but lights me up. Running cold hides me but costs speed and readiness. *(Not free, not symmetric — couples to movement and weapons.)*
- **Do I go ACTIVE** — ping with radar/lidar to flush out a cold, silent ship — knowing the ping **exposes me**? Or stay **passive** (read their heat, stay quiet, but be blind to anyone running cold)?
- **Whoever detects first chooses the fight** — pounce, or fade.

That's the prize: **fog of war + a heat/emissions tradeoff + active-to-flush + first-strike.** The rigorous EM-spectrum math is *not* the prize — it's the gauge needle, under the hood. (Sensors are also **components** with other purposes — survey, counter-interference — sequenced by what they connect to; see §3a.)

---

## 1. What EXISTS today (the survey)

The sensor subsystem (`GameEngine/Sensors/`) is **one of the most complete in the engine** — and **almost none of it is wired to a decision.** It is a fully-built ESM/RADAR sim with no operator controls and nothing downstream listening.

**The contact model (the "track table") — built, rigorous:**
- Every detectable entity carries a `SensorProfileDB`: `EmittedEMSpectra` (what it radiates — thermal/IR from reactors/engines = **passive signature**) and `ReflectedEMSpectra` (what bounces back when actively illuminated = **radar return**).
- Entities that can sense carry `SensorAbilityDB` (a receiver), defined by a `SensorReceiverAtb` (tuned wavelength, sensitivity, scan time).
- Detection runs in `SensorScan`: for each target, signal is attenuated by distance (inverse-square), then `DetectonQuality()` scores how well it overlaps the receiver's tuned band → a `SensorReturnValues` with **SignalStrength** and **SignalQuality (0–1: "something's there" → "full ID")**.
- Hits land in `SystemSensorContacts` — a **per-faction, per-system track table**. `StarSystem.GetSensorContacts(factionId)` answers *"what does this faction currently see?"*, including each contact's **last-known position** (which can lag reality — a real fog-of-war feature, already there).
- Contacts **persist through save/load**.

**The physics — built, arguably over-built:** emitted vs. reflected spectra, per-band absorption, triangular waveform overlap, an hourly `SensorReflectionProcessor` that updates radar returns "based on active illuminators nearby."

**Active vs. passive — HALF-built:** the receiver's tuned wavelength already decides whether it reads a target's **emissions** (passive) or its **reflections** (active radar). The substrate for the posture tradeoff is *there*.

## 2. How it WORKS (the signal chain, plain English)

1. Everything radiates (`EmittedEMSpectra`) — like a ship's heat plume. Bigger reactor/engine = louder.
2. The active-radar processor updates what each thing *reflects* when illuminated.
3. A receiver scans: for every target in the system, it asks "after this much distance, is the target's signal still above my sensitivity in my tuned band?" If yes → a contact, with a quality score.
4. Contacts drop into the faction's track table; the map UI reads it to draw blips.

**The damning part:** the scan is the *only* thing consuming the physics, and the track table is consumed by **only the map UI**. Two confirmed holes:
- **Combat is omniscient.** The battle trigger and fire control read `GetAllEntitiesWithDataBlob<FleetDB>()` / raw entities — **zero** references to the track table. You fight what's *present*, not what you *detect*. (Grep-confirmed: nothing in `GameEngine/Weapons/` touches contacts.)
- **The scan barely runs in tests.** `SensorScan` is only scheduled by `Game.PostNewGameInitialization()` (live New-Game path). It *does* run in the real game, but the test harness never kicked it — which is why the whole subsystem shipped 🔴 **DARK** (no gauge). **Slice-1 gauge (`SensorDetectionTests`) is now ✅ CI-GREEN** (commit `666d555`): a faction's ship detects a hostile ship at point-blank and the contact lands in the track table. So the engine genuinely works — the foundation fog-of-war rides on is verified, and Sensors is no longer dark.

## 3. KEEP / CUT / ADD / CONNECT (the decisions — mark these up)

*(Keep/Cut/Add judges the pieces; **Connect** is the one that matters — see the table, then the CONNECT block under it. The real test of detection is detection ON TOP OF weapons, not detection alone.)*

| | Item | Call | Why |
|---|---|---|---|
| **KEEP** | Per-faction contact track table (`SystemSensorContacts`) | ✅ keep, it's the spine | It already answers "what do I see"; fog of war rides straight on it. |
| **KEEP** | Emitted + reflected signature model; range attenuation | ✅ keep | This is the *substrate* of the EMCON tradeoff — don't rebuild it. |
| **KEEP** | Last-known-position lag on contacts | ✅ keep | Free fog-of-war texture (a stale track is a real tactical fact). |
| **KEEP** | Active vs. passive receiver concept | ✅ keep + surface it | Half the EMCON lever already exists at component level. |
| **KEEP (quietly)** | Contact **quality 0–1** | ✅ keep, use lightly in v1 | "blip vs. full ID" is good texture; but v1 gameplay can gate on *detected y/n* and treat quality as flavor until it earns more. |
| **CUT as gameplay** | EM-waveform spectrum / per-band absorption / triangular-overlap tuning | 🔇 **hide, don't delete** | Keep it computing the number; **never make the player tune wavelengths.** Per the weight rule: it adds complexity, not a decision. Collapse the player's mental model to "signature vs. sensor vs. range." |
| **CUT** | EW / jamming | ❌ not now | Net-new, not on the path. Parking lot. |
| **ADD** | **Fog of war in combat** | ➕ the seam | Battle trigger + fire control ask the track table "what hostile do I *detect*," not "what's present." An unseen enemy can't trigger or be targeted. *This is the core add.* |
| **ADD** | **The HEAT/emissions lever** ("how hot do I run") | ➕ the decision | Your signature scales with what you're DOING (thrust / hot guns / active sensors) plus an explicit "run silent" throttle. High = a beacon seen from far; cold = hidden but slower/less ready. Sets **your detectability + your capability** — *not* how far you see (that's the target's heat × your sensors). Needs the signature made **dynamic** (today it's a static design constant). |
| **ADD** | **Active-to-flush choice** (active vs. passive) | ➕ the decision | Passive = read their heat (blind to a cold ship, but quiet). Active = ping to find the cold/silent ship — but the ping **exposes you**. Reuses the existing emitted (passive) + reflected (active) halves; add the "pinging raises my signature" coupling. |
| **ADD** | **Reliable scan + gauge** | ➕ foundation | Make detection fire and be testable headless (slice 1) so everything above is gauged, not DARK. |
| **ADD** | **UI** | ➕ (your build) | Contacts drawn as *contacts* (fog), an EMCON toggle, detection-range ring. Client-side, live-tested. |

**CONNECT — the real test is the STACK, not the unit.** A system isn't "done" because it works alone — it's done when it *changes how the connected systems play*. Detection's real test is **detection on top of weapons**: does what you can SEE decide the fight? Map every wire, build the seam, gauge the *integrated* behavior:

| Detection connects to | The wire | The real-test gauge |
|---|---|---|
| **Weapons / battle trigger** | trigger reads the **track table**, not all entities | an **undetected** hostile does NOT start a battle; a **detected** one does |
| **Fire control / targeting** | can only target what's in the track table | you cannot order fire on an undetected entity |
| **Heat / active posture** | how-hot-you-run scales your **signature**; active-ping adds exposure | a **cold** fleet reads a hot picket and slips past unseen; a **hot/active** fleet is read from far; going **active** flushes a cold ambusher but lights *you* up |
| **Movement** | thrust state IS heat — burn hard = loud, coast = quiet | a hard burn to close fast announces you; a cold coast arrives unseen (the approach is the bet) |
| **Combat interrupt** | auto-pause fires when YOU first detect/engage | first contact hands you the doctrine call the moment info arrives |
| **Doctrine** | detect-first = set posture before contact | the side that sees first chooses the engagement |
| **Materials/data** *(the data end)* | sensor/EMCON components cost real materials; check the other end (gotcha #10) | a new sensor/EMCON design builds from defined, stocked materials — no JSON drift |

**The headline: fog-of-war gating the trigger (detection × weapons) IS the deliverable.** "It detects" (slice 1, green) is necessary, not the test. "What I can't see can't pull me into a fight, and what I choose to hide lets me strike first" is the test.

## 3a. Sensors are COMPONENTS — the multi-purpose design space (sequenced by Connect)

Sensors aren't one knob; they're **components you design and tune**. Per the developer (2026-06-26), the design space is **FOUR sensor flavors** — distinct components with distinct use cases:

| Flavor | What it's for | The tradeoff (must be real, or it's menu-bloat) | Connects to | Status |
|---|---|---|---|---|
| **Long-range** | early warning — spot the hot enemy from far | far reach, **coarse** (low quality/ID), heavier/pricier | combat (the picture) — **now** | a tuned variant of the existing `passive-sensor` |
| **Short-range** | fire-control-grade detail up close | **sharp** (high quality/ID), short reach, cheap/light | combat (targeting) — **now** | `passive-sensor` variant |
| **Detection** | the general-purpose military sensor | balanced middle | combat — **now** | the existing `passive-sensor` itself |
| **Survey** | read planets/bodies (habitability, minerals) — *not* ships | a different job entirely (no combat use) | survey → mining/colonization | **already exists**: `geo-surveyor` + `gravitational-surveyor` components |

So **3 of the 4 are military-detection variants** that connect to weapons now (the `passive-sensor` is the seed; long-vs-short is a genuine reach↔detail trade that stacks with the heat model — a long-range picket spots a hot fleet first, a short-range sensor gives the firing solution); **the 4th (survey) already exists** and connects to the survey/mining mechanic. All four are cradle-to-grave-grounded *today* (components, researched, built). **The caveat that keeps it honest:** each flavor must be a *distinct decision* (real reach/quality/cost trades), not four near-identical sensors.

Under those flavors sit the underlying *capabilities/purposes*, sequenced by Connect — build each only once the thing it connects to exists:

| Sensor purpose | What it is | Connects to | Build when |
|---|---|---|---|
| **Military detection + heat** | find ships; "how hot you run" | **weapons (combat)** — live now | **M1, now** |
| **Active illumination** | ping to flush cold/silent ships, exposing you | **combat / heat lever** — live now | **M1, now** |
| **Counter-interference** | a sensor tuned to **see through** an obscuring cloud | needs **obscuring hazards** (don't exist yet) | when hazards exist — substrate (per-band absorption) is already there; **leave the seam** |
| **Diagnostics** | detect *why* you've gone blind ("it's a neutrino cloud, not a fault") | needs hazards too | with counter-interference |
| **Survey / habitability** | read a planet's atmosphere/temp for colonization | **colonization / eXplore** (M2 / v2) | when colonization is a decision — reuse the existing `GeoSurveys/` ability |

The rule that keeps this from ballooning: **don't build a sensor purpose until the system it connects to is live.** A "see-through-the-cloud" sensor with no clouds is the definition of *pretty*. So M1 builds the two purposes that connect to weapons today; the rest are **component-type seams we design now and fill when their partners exist** — that's how the rich design space lands without half-building it.

## 3b. EMCON — the variables, traced (variable → system? → player can tweak? → player cares?)

EMCON is **not one variable** ("how hot you run") — it's a cluster of emitter systems you already own. Run each through the checklist (this is cradle-to-grave + earns-its-weight as a question set — *the standing way to think about any feature*):

| Variable (makes you loud / lets you see) | System behind it — exists? | Player can tweak? | Would the player care? (the decision) | Verdict |
|---|---|---|---|---|
| **Thrust / burn** | engine component + the move order | design (engine) + **play (burn vs coast)** | YES — close fast & light up, or coast cold & sneak | **BUILD** (headline) |
| **Reactor output** | reactor — *already* the signature driver (`SensorSignatureAtb` = reactor CoreOutput), but **static** today | design (reactor) + **play (throttle / run-silent)** | YES — bigger reactor = more guns/sensors but louder | **BUILD** (make dynamic) |
| **Weapons firing** | weapon component + fire state | design + **play (fire vs hold)** | YES — the first shot gives away the ambush | **BUILD** |
| **Active sensors** | active-sensor component + active/passive toggle | design + **play (ping vs quiet)** | YES — light the room to find the cold guy, get seen | **BUILD** |
| **Sensor reach vs detail** | the receiver (the 4 flavors, §3a) | **design** | YES — picket sees far/coarse; short-range gives the firing solution | **BUILD** |
| **EMCON posture** | a fleet order (new) — master switch over the four above | **play** | YES — the one dark/loud lever | **BUILD** (the lever) |
| **Hull cross-section** | ship size (`TargetCrossSection` from radius) — exists | design (sized for other reasons) | passively — a capital is *inherently* a beacon | **KEEP** (falls out; no dial) |
| **Range / distance** | position + movement (inverse-square) | play (where you sit) | YES, but automatic | **KEEP** (core mechanic; no dial) |
| **Heat sinks / radiators** | a thermal-store component — *likely absent* | design (if built) | YES — run hot, stay cold, dump heat after the strike | **LATENT** (new component) |
| **Stealth coatings / RAM** | per-band absorption (substrate exists) → a material/hull flavor | design (a stealth hull) | YES — a real stealth-ship design tradeoff | **LATENT** (material flavor) |
| **Environmental masking** | star emissions modeled; masking + clouds aren't | play (lurk near a star / in a cloud) | YES — positional stealth | **LATENT** (cloud/hazard seam) |
| **Contact quality** | sensor resolution (0–1, exists) | design | a little — ID detail | **KEEP-as-flavor** v1; deepen later |

**Insight:** the four emitters (engine / reactor / weapons / active-sensor) are **existing components** — so EMCON is *wiring their activity into a dynamic signature + one posture order*, **not a new system** (cradle-to-grave by construction; the reactor is *already* the signature driver). Today the signature is a **static** design number; **making it dynamic (thrust on, guns firing, sensors active → louder) is the actual build.** The "would the player care" filter split the cluster cleanly: emitters + sensor flavor + posture = *felt* decisions (build); cross-section + range = *automatic* (keep, no dial — a "cross-section slider" would be pretty); heat sinks / coatings / masking = real depth but **latent** until their systems exist.

**So slice 3 = make the signature DYNAMIC from the four emitters' activity + add the EMCON posture order (the master dark/loud switch).** Open call: one bundled posture (**Full / Cruise / Silent** — the Navy EMCON condition) vs. four separate switches. *Lean: bundled — one legible knob — with the rule that **firing weapons always lights you up** regardless (you can't shoot quietly).*

## 3c. EMCON, grounded in the code — EXISTS / MISSING / NEEDS-CHANGE (investigated 2026-06-26, file:line)

§3b flagged these systems from memory; here's what's **actually in the files**. **Headline: EMCON v1 is finishing INERT SCAFFOLDING + a few small wires — not new subsystems.** The original author scaffolded dynamic-signature *and* active-sensor events and left them unwired.

| System (EMCON variable) | What's actually there | Verdict | File |
|---|---|---|---|
| **Signature store/compute** | `SensorProfileDB.EmittedEMSpectra`, set **ONCE** at component install (`SetProfileDB`), never updated at runtime | **EXISTS but STATIC** — the thing to make dynamic | `SensorProfileTools.cs:14-45` |
| **Dynamic-signature hook** | `EMData.StateLoad` getter **already reads** `ComponentInstance.ComponentLoadPercent` — but load is **never set** (so it's always 100%) and nothing scales by it | **INERT SCAFFOLDING** — finish it | `SensorProfileDB.cs:102-110`, `ComponentInstance.cs:77` |
| **Reactor → signature** | uses **design-time** output; the **live** `EnergyGenAbilityDB.Output` exists + varies with load but is **never consulted** | **NEEDS-CHANGE** — scale by live load | `EnergyGenAbilityDB.cs:44`, `energy.json:74` |
| **Engine/thrust → signature** | engines **do** carry `SensorSignatureAtb` (design Thrust); but **no runtime "burning now" state** | **NEEDS-CHANGE** — add a current-thrust state | `engines.json:82`, `NewtonThrustAbilityDB.cs` |
| **Weapons firing → signature** | weapons carry **NO** signature (only missiles do); **no "firing this tick" state** (`ThermalOutput_W` is fire-rate-limit only) | **NEEDS-BUILD** (small) — firing spikes you | `GenericBeamWeaponAtb.cs` |
| **Active sensors → exposure** | reflections computed; passive/active by tuned wavelength — but pinging **does NOT raise your own signature**; unused `ActiveSensorDetected` event | **HALF-BUILT** — add the exposure | `SensorReflectionProcessor.cs`, `EventTypes.cs:310` |
| **Reactor throttle / run-silent** | output fixed at max−demand; **no throttle, no power/EMCON order** | **NEEDS-CHANGE** — add `PowerThrottlePercent` | `EnergyGenProcessor.cs:24` |
| **EMCON posture/order** | none — no order, no state, no flag | **NEEDS-BUILD** (small, mirrors doctrine) | — |
| **Cross-section** | π·r² from ship radius; fixed geometry | **EXISTS, no lever** (keep) | `SensorProfileDB.cs:33-47` |
| **Contact quality** | computed 0–1; **consumed for SURVEY detail** (star/planet accuracy gating), **not combat** | **EXISTS, survey-wired** — ties to the *survey* flavor; v2 for fire-control lock | `SensorReturnValues.cs:8`, `SystemBodyInfoDB.cs:154` |
| **Heat / radiators** | **none** on ships (thermal math lives only in the asteroid-damage sim) | **v2 / LATENT** — not needed for EMCON v1 ("hot = loud now") | `Damage/DamageVeryComplex/` |
| **Environmental masking** | **none** — detection is pure signal-vs-sensitivity; **author's own TODO wants it** ("ships near a sun hidden") | **v2 / LATENT** (positional stealth) | `SensorTools.cs:65` |
| **Stealth-by-material** | emitter side is a flat `Reflectivity 0.9`; per-band material math **exists but only in the damage path** | **v2 / LATENT** (reuse the damage math) | `SensorProfileDB.cs:53`, `ArmorBlueprint.cs` |

**Slice 3 build list (the wires, grounded):** (1) ✅ **DONE** — `SensorProfileDB.ActivityMultiplier` read in the detection math (`SensorTools.AttenuatedForDistance` + `AttenuatedForDistanceList`), scaling EMITTED only; default 1.0 so no behavior change until a driver sets it. Gauged by `SensorEmconTests`. (2) ✅ **DONE** — `EmconActivityProcessor` (hotloop, keyed to ShipInfoDB) sets `ActivityMultiplier = posture-base × HeatFactor(thrust, firing)`. Reactor-load deferred (the `Load` field is buggy — see §6). Gauged by `SensorEmconTests`; (3) ✅ **DONE** — `FleetEmconDB` + `FleetEmcon.SetPosture` (**Full / Cruise / Silent**, mirrors doctrine; direct call, pushes the scale onto member ships). Gauged by `SensorEmconTests`. *(The processor that combines this with reactor/thrust/firing is item (2), next.)* (4) `PowerThrottlePercent` on `EnergyGenAbilityDB` so Silent caps output; (5) ✅ **DONE for free** — no new runtime burn/firing STATE was needed: thrust reads the existing `NewtonMoveDB.ManuverDeltaVLen`, firing the existing `GenericFiringWeaponsDB.ShotsFiredThisTick` (folded into wire 2's processor); (6) active-ping adds to your own `EmittedEMSpectra`. Each its own gauged slice.

**v2 / latent (genuinely new, correctly deferred):** heat sinks/radiators (the juicy "store heat, dump after the strike"); environmental masking (the *author's own TODO* — hide near a star); stealth-by-material (reuse the damage-path per-band math on the emitter side). None block EMCON v1.

## 4. How it all COMES TOGETHER (the target)

One sentence: **how hot you run sets how far you're SEEN (and how fast/ready you are); how far you SEE is the target's heat × your sensors; combat only acts on what you detect — so a cold ship that spots a hot one first owns the engagement.**

The loop, and how it **stacks** with what we already built:
- **Heat/emissions lever** → hot (burn / hot guns / active sensors) = fast, strong, a beacon; cold = hidden but slower/less ready. The bet is *exposure vs. capability*. **Active-to-flush:** can't find a cold ship passively? Ping it — and accept the ping paints you for everyone.
- **Fog of war (the seam)** → the battle trigger only auto-engages hostiles in your **track table**. A cold fleet can slip past a hot picket; a hot fleet trips every picket on the way in.
- **First-strike** → because engagement needs detection, the side that detects first **chooses**: pounce, or fade. **Stacks with the combat interrupt** — the auto-pause fires when *you* first detect/engage, handing you the doctrine call the moment information arrives.
- **Doctrine** → detect-first means you set posture *before* contact. Detection feeds the lever we already made earn its weight.
- **Movement** → thrust IS heat: a hard burn closes fast but announces you; a cold coast arrives unseen. Detection + movement = the approach is the gamble.

The rigorous EM physics stays the **under-the-hood number** the heat/sensor model scales — present, never something the player reads.

## 5. Open calls for YOU (before I write the build slices)

1. **The heat lever — emergent or discrete?** Does "how hot you run" ride on what you're DOING (thrust / hot guns / active sensors auto-raise your signature) + an explicit "run silent" throttle, OR a discrete stance (Full / Cruise / Silent)? *(Lean: a discrete stance for legibility, with signature also nudged by activity underneath.)*
2. **Per-fleet or per-ship?** Rec: **per-fleet** for v1 (matches how doctrine works), per-ship later.
3. **Active sensors — a deliberate toggle?** Going active flushes hidden/cold ships but exposes you. Rec: **deliberate, default passive** — choosing to light up to find what's hiding *is* the decision.
4. **Quality in v1: gate info, or flavor?** Rec: **flavor** for v1 (detected = you can engage); make quality gate detail later, so we don't front-load complexity.
5. **Multi-purpose sensor components in M1?** Build military-detection + heat + active/passive now, and leave **cloud-penetration / "why am I blind" / survey-habitability** as *architected-but-unbuilt seams* (they need obscuring hazards / the colonization layer to connect to). Rec: **yes — seams now, content when their connections exist** (see §3a).
6. **EW/jamming stays parked?** Rec: yes.

## 6. Build sequence — CRADLE TO GRAVE, each a bounded gauged slice (once §3/§5 signed off)

First the **cradle-to-grave trace** — the acceptance test (root `CLAUDE.md` → "Cradle to Grave"): name every rung and mark what exists vs. needs building. Detection rides the component architecture (`CONVENTIONS.md` §6), so most rungs are *there*:

| Rung | Detection's version | Status |
|---|---|---|
| **mineral → material → production** | sensor/EMCON components cost materials, built at a colony | exists, but on **raw minerals** — the refined-material rung is **economy lever #1's job (shared)** |
| **component (designed)** | `SensorReceiverAtb` + a new signature/**EMCON** `*Atb`, in the component designer | sensor ✓; an EMCON/signature-reduction component = **ADD** |
| **research-gated** | the **sensors** tech category gates sensor/EMCON designs | category exists ✓; tie new EMCON designs to it |
| **installed on unit/building** | sensors sit on ships (and can on stations) | ✓ |
| **in-play decision** | fog-of-war + heat/active **posture** (a fleet ORDER like doctrine; capability *bounded by the components*) | **ADD** |
| **grave (loss)** | a **destroyed sensor component blinds you** — detection × damage | **ADD** (wires to the damage system) |

The slices, in order:
1. **Gauge the engine** ✅ **DONE (`666d555`)** — a fleet detects a hostile fleet; Sensors DARK → verified.
2. **Fog-of-war seam — THE real test (detection × weapons). ✅ BUILT (CI pending).** The battle trigger now gates on the track table (`CombatEngagement.RequireDetectionToEngage` + `FleetDetects` reading `GetSensorContacts`) — two hostile fleets engage only if they DETECT each other (v1 = mutual; ambush asymmetry is a later slice). Behind a default-off flag (like the interrupt) so existing combat fixtures stay deterministic; **off in the client too** until live-tested. Gauge: `BattleTriggerTests.Tick_RequireDetection_NoBattleUntilDetected` — no scan → no battle; scan → battle. *(Engine-side.)*
3. **Heat/EMCON as COMPONENTS + a posture ORDER.** The *capability* (signature level, run-silent, active-ping) lives on **components** (researched/built/installed — §6 hands you that chain free); the *choice* is a fleet order (`FleetEmconDB`, like doctrine). Make the signature **dynamic**. CI-gauge: a cold ship is detected at shorter range than a hot one, AND a fleet with a better EMCON component runs quieter — capability comes from the components, *not a free flag*. *(Engine-side.)* **Built in sub-wires (3a–3f, list below); each its own gauged slice:**
   - **3a — dynamic-signature foundation ✅ BUILT (CI green, `0117570`).** `SensorProfileDB.ActivityMultiplier` (default 1.0 = as-designed) is now read in the detection math: `SensorTools.AttenuatedForDistance` **and** `AttenuatedForDistanceList` scale the **EMITTED** spectra by it and leave the **REFLECTED** (radar-return) spectra alone — going quiet doesn't shrink your hull. Default 1.0 keeps slices 1 & 2 byte-for-byte unchanged. This finishes the dynamic-signature the original author scaffolded (the EMITTED-spectra comment + the inert `EMData.StateLoad`). Gauge: `SensorEmconTests.ActivityMultiplier_ScalesEmittedSignature_NotReflected` (dict path) + `_ListPath` — dark (0.1) quiets the emitted band to one-tenth, reflected unchanged.
   - **3b — the EMCON posture LEVER ✅ BUILT (CI pending).** `FleetEmconDB` (Full/Cruise/Silent, mirrors `FleetDoctrineDB`) + `FleetEmcon.SetPosture(fleet, posture)` — a **direct call** like doctrine (usable mid-combat) that pushes the posture's signature scale (1.0 / 0.5 / 0.15) onto every member ship's `ActivityMultiplier`, recursing sub-fleet components. v1 PUSH model (instant feedback on the order); a ship joining later keeps its default until 3c's processor reconciles — flagged, not silent. Lives in `Sensors/Emcon/` (depends on Fleets/Ships, NOT the combat resolver). Gauge: `SetPosture_PushesScaleToAllMemberShips_RecursingComponents` (push + recursion + reversibility + scope) and `FleetPosture_FlowsIntoTheRealDetectionMath` (the stack — posture → ship dial → the real `AttenuatedForDistance` the scan uses). *No runtime-activity inputs yet (3c+), so a fleet emits at its posture scale flat.*
   - **3c — signature responds to ACTIVITY ✅ BUILT (CI pending).** `EmconActivityProcessor` (hotloop, 5 s, keyed to **ShipInfoDB** — NOT SensorProfileDB, which `SensorReflectionProcessor` owns; one hotloop per DataBlob type) sets `ActivityMultiplier = SignatureBaseMultiplier × HeatFactor(burning, firing)`. **Thrust** read from `NewtonMoveDB.ManuverDeltaVLen > 0` — the investigation killed a planned risky change here: there's **no need for an `IsBurning` flag**, the burn state already lives in the move blob (it's the gate on thrust in the integrator). **Firing** from `GenericFiringWeaponsDB.ShotsFiredThisTick`. Heat math + posture×activity composition CI-gauged (`SensorEmconTests.HeatFactor_*`/`ComputeActivityMultiplier_*`/`Processor_OnIdleFleet_*`); the real burning/firing reads are **live**-verified (standing up a genuinely thrusting/firing ship in a unit test is fragile, and the processor auto-runs, so flying/firing live exercises the Movement↔Sensors / Weapons↔Sensors links). Emergent: a lit drive plume betrays you even on Silent.
   - **Investigation correction (the standing process earned its keep):** the prior note here claimed reactor `Load` was "read-ready at `EnergyGenAbilityDB.cs:38`." **That was wrong** — going to the source (`EnergyGenProcessor.cs:43-52`) showed `Load = TotalOutputMax / batteryInflow`, inverted and unbounded (1.0 at idle, →∞ under demand), NOT a 0-1 "percent of max." Thrust + firing (the clean dominant signals) shipped first; reactor-load was deferred. **Update: that `Load` bug is now FIXED (slice D — `EnergyGenProcessor.CalcLoad` = `Demand/TotalOutputMax` clamped 0-1), which also stopped idle reactors burning near-max fuel.** Folding reactor-load into the heat factor is now unblocked (still unwired — marginal vs thrust).
   - **3f + reactor + follow-ups (next):** active-ping self-exposure (you can't ping quietly); reactor-load once the `Load` bug is sorted; `PowerThrottlePercent` so Silent caps reactor output; *switch cooldown (reactor thermal inertia) = flagged candidate.*
4. **Grave rung — a destroyed sensor blinds you. ✅ BUILT (CI pending).** Lose your sensor receivers → the ship's receiver cache empties and it stops scanning (detection × damage). Wired by hooking `SensorTools.SetInstances` to `ReCalcProcessor.TypeProcessorMap[SensorAbilityDB]` — the damage system calls `ReCalcAbilities` after destroying a component, which now rebuilds the sensor cache from live components (and `SetInstances` was hardened to CLEAR the cache when zero receivers remain, the case it previously skipped). CI-gauge: `SensorDetectionTests.DestroyingSensor_BlindsTheShip_GraveRung` — remove the receivers + recalc → cache empty → next scan detects nothing. *v1 limit: already-recorded contacts persist until they age out (no contact-expiry pass yet — flagged).* *(Engine-side.)*
5. **First-strike ✅ BUILT (CI pending).** The detected-first side shoots first. The engage gate went mutual→`FleetDetects(a,b) || FleetDetects(b,a)` (a detector opens fire on a still-blind target), and `StepEngagementGroup` now builds **directed** fire — `targetsOf`/`attackersOf` via `CanEngageTarget(attacker, target)` (fog-off → always, so the symmetric resolve and every combat fixture are unchanged; fog-on → attacker must DETECT target). A blind fleet takes fire without returning it. CI-gauge: `BattleTriggerTests.FirstStrike_SeerWipesBlindEnemy_Unscathed` — two EQUAL armed fleets, the enemy blinded via the grave-rung path, fog on → the player wipes it taking **zero** losses (equal forces both firing = mutual kill, so player-intact + enemy-wiped proves the asymmetry). Composes detection × grave-rung × weapons. *(Engine-side.)*
6. **UI** — contacts as blips, the EMCON order, range ring, and the sensor/EMCON **component in the designer** (research → build). *(Your local build; CI can't see the client.)*

Engine slices (2–5) I build + CI proves; the UI (6) is yours. One at a time, each green before the next. **The one shared rung** — sensor/EMCON components costing **refined** materials — lands with economy lever #1; until then they ride the existing raw-mineral cost (a *flagged, deliberate* gap, not a skip).
