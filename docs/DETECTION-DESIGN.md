# Detection Design — what exists, what we keep/cut/add, how it comes together

*Draft 2026-06-26 — the design pass BEFORE we build. Detection is M1 lever #1 (`docs/MVP.md`, `docs/REALISM-VS-GAMEPLAY-AUDIT.md`). This doc is for marking up: the Keep/Cut/Add calls in §3 and the open questions in §5 are proposals, not settled.*

---

## 0. The point first (what detection is FOR)

In Navy terms you already own: detection is the **ESM/RADAR + EMCON** game. The gameplay isn't the physics of how a receiver tunes a band — it's the **decision** the physics forces on the player:

- **Do I even know the enemy is there?** (fog of war)
- **Do I light off active radar** — see far, but broadcast my position and get seen first — **or run EMCON-dark** — listen only, see short, but slip in and shoot first?
- **Whoever detects first chooses the fight** — engage, or fade away.

That's the whole prize: **fog of war + an emissions-posture tradeoff + first-strike.** Everything below serves those three. The rigorous EM-spectrum math is *not* the prize — it's the gauge needle, and it can stay under the hood.

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
| **ADD** | **The EMCON lever** (Active / Passive-dark posture) | ➕ the decision | A fleet/ship stance that sets **detection range** AND **own detectability**, reusing emitted/reflected. Active = see far, get seen far; Dark = see short, stay quiet, ambush. |
| **ADD** | **Reliable scan + gauge** | ➕ foundation | Make detection fire and be testable headless (slice 1) so everything above is gauged, not DARK. |
| **ADD** | **UI** | ➕ (your build) | Contacts drawn as *contacts* (fog), an EMCON toggle, detection-range ring. Client-side, live-tested. |

**CONNECT — the real test is the STACK, not the unit.** A system isn't "done" because it works alone — it's done when it *changes how the connected systems play*. Detection's real test is **detection on top of weapons**: does what you can SEE decide the fight? Map every wire, build the seam, gauge the *integrated* behavior:

| Detection connects to | The wire | The real-test gauge |
|---|---|---|
| **Weapons / battle trigger** | trigger reads the **track table**, not all entities | an **undetected** hostile does NOT start a battle; a **detected** one does |
| **Fire control / targeting** | can only target what's in the track table | you cannot order fire on an undetected entity |
| **EMCON posture** | posture scales your detect-range + your signature | **Dark** fleet slips a picket (sees short, unseen); **Active** trips it (sees far, seen far) |
| **Combat interrupt** | auto-pause fires when YOU first detect/engage | first contact hands you the doctrine call the moment info arrives |
| **Doctrine** | detect-first = set posture before contact | the side that sees first chooses the engagement |
| **Movement** *(later lever)* | dark+slow = ambush; active = picket/tripwire | a scout's posture changes what a fleet reveals |
| **Materials/data** *(the data end)* | sensor/EMCON components cost real materials; check the other end (gotcha #10) | a new sensor/EMCON design builds from defined, stocked materials — no JSON drift |

**The headline: fog-of-war gating the trigger (detection × weapons) IS the deliverable.** "It detects" (slice 1, green) is necessary, not the test. "What I can't see can't pull me into a fight, and what I choose to hide lets me strike first" is the test.

## 4. How it all COMES TOGETHER (the target)

One sentence: **you pick an emissions posture; that sets how far you see and how far you're seen; combat only acts on what you detect; so seeing first is a real advantage you bet on.**

The loop, and how it **stacks** with what we already built:
- **EMCON posture (the lever)** → sets your detection range + your signature. *Two postures to start* (Active / Dark); maybe a middle default.
- **Fog of war (the seam)** → the battle trigger only auto-engages hostiles in your **track table**. Run dark and you can slip a fleet past a picket; run active and you'll see them coming but they'll see you.
- **First-strike** → because engagement needs detection, the side that detects first **chooses**: pounce, or fade. That decision **stacks with the combat interrupt** — the auto-pause fires when *you* first detect/engage, handing you the doctrine call at the exact moment information arrives.
- **Doctrine** → detecting first means you set posture *before* contact. Detection feeds the decision we already made earn its weight.
- **Movement (later lever)** → a dark fleet on a slow approach is the ambush; an active fleet is the tripwire/picket. Detection + movement = the scouting game.

The rigorous physics stays as the **under-the-hood number** the posture scales — present, but never a thing the player has to read.

## 5. Open calls for YOU (before I write the build slices)

1. **How many EMCON postures?** My rec: **two** — **Active** (see far / seen far) and **Dark/Passive** (see short / quiet). A third "balanced" default is easy but is it worth the menu? *(One knob that matters beats three that blur.)*
2. **Per-fleet or per-ship posture?** Rec: **per-fleet** for v1 (matches how doctrine works), per-ship later.
3. **Does going Active auto-trigger in a fight, or stay a deliberate choice?** Rec: **deliberate** — choosing to stay dark mid-fight (and shoot blind-ish) vs. lighting up is exactly the decision.
4. **Quality in v1: gate info, or flavor?** Rec: **flavor** for v1 (detected = you can engage); make quality gate detail later, so we don't front-load complexity.
5. **Scope check:** EW/jamming stays parked? (Rec: yes.)

## 6. Build sequence (once §3/§5 are signed off) — each a bounded, gauged slice

1. **Gauge the engine** ✅ **DONE (CI-green, `666d555`)** — a fleet detects a hostile fleet; Sensors DARK → verified.
2. **Fog-of-war seam — THE real test (detection × weapons).** The battle trigger consumes the track table (`GetSensorContacts`) instead of raw entities; an undetected hostile doesn't engage. CI-gauge: out-of-range hostile → no battle; detected → battle. Slice 1 ("it detects") was only the precondition — *this* is where detection earns its weight by changing combat. *(Engine-side, I can do solo.)*
3. **EMCON lever** — a `FleetEmconDB` / posture enum that scales detection range + emitted signature, reusing the existing model. CI-gauge: Dark detects/ is-detected at shorter range than Active; the asymmetry holds. *(Engine-side.)*
4. **First-strike falls out** — verify the detected-first side gets the interrupt/initiative; gauge the asymmetry. *(Engine-side.)*
5. **UI** — contacts as blips, EMCON toggle, range ring. *(Your local build; CI can't see the client.)*

Slices 1–4 are engine + CI (I build, CI proves); slice 5 is yours at the keyboard. We do them in order, one at a time, each green before the next.
