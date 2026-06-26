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

Sensors aren't one knob; they're **components you design and tune**, and the sci-fi design space is real and *applicable here*. Per Connect, each purpose earns its keep only once the thing it connects to exists — so we **architect the component layer to accept all of them, and build each when its connection is live:**

| Sensor purpose | What it is | Connects to | Build when |
|---|---|---|---|
| **Military detection + heat** | find ships; "how hot you run" | **weapons (combat)** — live now | **M1, now** |
| **Active illumination** | ping to flush cold/silent ships, exposing you | **combat / heat lever** — live now | **M1, now** |
| **Counter-interference** | a sensor tuned to **see through** an obscuring cloud | needs **obscuring hazards** (don't exist yet) | when hazards exist — substrate (per-band absorption) is already there; **leave the seam** |
| **Diagnostics** | detect *why* you've gone blind ("it's a neutrino cloud, not a fault") | needs hazards too | with counter-interference |
| **Survey / habitability** | read a planet's atmosphere/temp for colonization | **colonization / eXplore** (M2 / v2) | when colonization is a decision — reuse the existing `GeoSurveys/` ability |

The rule that keeps this from ballooning: **don't build a sensor purpose until the system it connects to is live.** A "see-through-the-cloud" sensor with no clouds is the definition of *pretty*. So M1 builds the two purposes that connect to weapons today; the rest are **component-type seams we design now and fill when their partners exist** — that's how the rich design space lands without half-building it.

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
2. **Fog-of-war seam — THE real test (detection × weapons).** The battle trigger consumes the track table (`GetSensorContacts`) instead of raw entities; an undetected hostile doesn't engage. CI-gauge: out-of-range hostile → no battle; detected → battle. *(Engine-side.)*
3. **Heat/EMCON as COMPONENTS + a posture ORDER.** The *capability* (signature level, run-silent, active-ping) lives on **components** (researched/built/installed — §6 hands you that chain free); the *choice* is a fleet order (`FleetEmconDB`, like doctrine). Make the signature **dynamic**. CI-gauge: a cold ship is detected at shorter range than a hot one, AND a fleet with a better EMCON component runs quieter — capability comes from the components, *not a free flag*. *(Engine-side.)*
4. **Grave rung — a destroyed sensor blinds you.** Lose your sensor components → you drop out of the track-table game (detection × damage). CI-gauge: kill the sensor component → that faction's contacts go dark. *(Engine-side.)*
5. **First-strike falls out** — the detected-first side gets the interrupt/initiative; gauge the asymmetry. *(Engine-side.)*
6. **UI** — contacts as blips, the EMCON order, range ring, and the sensor/EMCON **component in the designer** (research → build). *(Your local build; CI can't see the client.)*

Engine slices (2–5) I build + CI proves; the UI (6) is yours. One at a time, each green before the next. **The one shared rung** — sensor/EMCON components costing **refined** materials — lands with economy lever #1; until then they ride the existing raw-mineral cost (a *flagged, deliberate* gap, not a skip).
