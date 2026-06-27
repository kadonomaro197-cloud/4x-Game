# The Information Delta — what the game KNOWS vs. what it TELLS the player

*Written 2026-06-27. The problem: the simulation computes a number, uses it, and never shows it — so the player
flies, fights, and burns fuel blind. This doc is the survey (EXISTS/MISSING ledger, with file:line) plus the
first build that closes part of the gap: engagement range, detection range, delta-V, and ETA made visible.*

---

## The one idea

**"It isn't communicating with the player" is two different problems wearing one mask.** Telling them apart is the
whole game:

- **Failure A — the gauge exists, nobody ran a wire to the dial.** The engine computes the number and even *acts*
  on it, but it never reaches the screen. Fix = a wire (read the value, draw it). Cheap.
- **Failure B — there is no gauge to wire.** The engine never computes the number the player needs *as a number*.
  You can't display a reading nobody took. Fix = build the gauge first (compute the number), *then* wire it. Less
  cheap, and it's an **engine** job before it's a UI job.

This is the Visibility Gate (`CLAUDE.md`) pointed at the player instead of at us: *you cannot control what you
cannot measure.* The engine room is fully instrumented; the gauge glass is painted over.

---

## The ledger (survey, 2026-06-27)

| Number the player needs | Does it EXIST in the engine? | Where | Was it shown? | Failure |
|---|---|---|---|---|
| **Beam engagement range** | ✅ yes — `GenericBeamWeaponAtb.MaxRange`, and the firing processor *enforces* it | `Weapons/WeaponBeam/GenericBeamWeaponAtb.cs:17,107` · `WeaponGeneric/GenericFiringWeaponsProcessor.cs:54` | ❌ nowhere | **A** (wire it) |
| **"Out of range" feedback** | ✅ the check runs every tick | `GenericFiringWeaponsProcessor.cs:54` | ❌ the gun just silently didn't fire | **A** (surface the state) |
| **Detection range ("how far can I see")** | ❌ NO — the scan only asks "detect at *current* distance?" (yes/no) | `Sensors/SensorRecever/SensorTools.cs:18,341` | ❌ impossible to show — no number | **B** (build the gauge) |
| **Delta-V remaining (the real range limit)** | ✅ yes — `NewtonThrustAbilityDB.DeltaV` (live field) | `Movement/NewtonMove/NewtonThrustAbilityDB.cs` | ❌ never displayed | **A** (wire it) |
| **Warp ETA / arrival time** | ✅ yes — `WarpMath.GetInterceptPosition(...).etiDateTime` | `Movement/WarpMove/WarpMath.cs:96` | ❌ warp window showed eccentricity, not when you arrive | **A** (wire it) |
| **Movement RANGE ring** | ⚠️ meaningless here on purpose | — | — | not-a-ring (see below) |

### Why there is no detection-range number (the important one)

Detection works like a flashlight in fog, not a radar fence. For each target the scan takes its signature (how
loud it is, in kW), dims it by distance with the inverse-square law (`signal = power / (4π·d²)`,
`SensorTools.cs:341`), and asks **"is the dimmed signal still above my sensor's threshold?"** — a *yes/no at the
target's current distance*, computed fresh every scan. It never asks "how far away could a ship like this be
before I'd lose it?" So there is **no `DetectionRange` field anywhere** — and that's *correct* design: how far you
see depends on how loud the *other* guy is. A ship running dark you catch at knife-range; the same ship running
hot you see across the system. Range is a *relationship between two ships*, not a property of one. To draw a ring
you must first **decide what range means** (against what target) and then run the attenuation **backwards**.

### Why movement is NOT a range ring (the developer was right)

Movement is **goal-based, not distance-based.** Newtonian thrust is continuous burn limited by *fuel*, not by a
fence — the ship can reach any direction to any distance given enough delta-V and time; what's limited is the
*cost* (delta-V) and the *time* (burn + coast). Warp doesn't move toward a clicked point at all — it moves to a
*body's orbit*. So a "movement range ring" would be actively misleading. The right readouts are **delta-V remaining**,
**ETA to target**, and **maneuver fuel cost** — all of which the engine already computes and threw away before the
screen (`WarpOrderWindow` even read the ship's max delta-V into `_maxDV` and never printed it).

---

## What was built (2026-06-27)

**Engine layer (CI-covered — the gauges):**
- `WeaponUtils.GetMaxBeamRange_m(ship)` / `GetBeamWeaponRanges(ship)` — a ship's beam reach, aggregated across its
  installed, working weapons (a shot-off gun no longer counts — the loss rung). Failure A surfaced.
- `SensorTools.RangeForSignal(source, threshold)` — the exact inverse of the attenuation the scan uses
  (`d = √(source / 4π·threshold)`). The gauge that didn't exist (Failure B).
- `SensorTools.DetectionRange_m(receiver, target)` — how far that receiver first picks up that target (loudest band
  wins; emitted scales with the target's EMCON activity, reflected doesn't — the dark-vs-loud truth).
- `SensorTools.SelfDetectionRange_m(ship)` — "a ship like me, running as I am, I'd first see at range R." Uses the
  ship's *own* signature as a magic-constant-free reference; because it reads live `ActivityMultiplier`, the ring
  **shrinks on Silent and grows running hot** — the EMCON lever made visible.
- Gauged by `Pulsar4X.Tests/RangeReadoutTests.cs` (weapon aggregation on the Aegis; the attenuation round-trip;
  loudest-band/activity scaling; the self-ring shrinking on Silent end-to-end through the real posture path).

**Client layer (CI-blind — the wires; verify on the local build):**
- **Fleet Combat tab:** a fleet "Beam reach" row + per-ship "Beam Range" / "Sensor Reach" columns; and a
  **"Show range rings on map"** toggle that draws beam reach (red) + sensor reach (blue) circles on the system map
  (reusing `SimpleCircle` + `UIWidgets`, the DebugWindow "Draw SOI" mechanism — no new SDL code).
- **Fire Control:** range-to-target vs. the ship's beam reach, with a red **OUT OF RANGE** flag — the fix for the
  silent no-fire.
- **Warp Order:** "Available Δv" + "ETA / arrive" at top level, from values the window already computed.

---

## Live-test gauges — filling the hole CI can't see

CI builds the engine + tests but **never compiles the SDL client**, so it cannot tell whether a client *wire*
reads the right value or fires at all — only the engine *gauge* (the accessor) is CI-checkable. So every client
wire here also drops a **`SessionLog` line at its action/commit point** (flushed immediately into the `game_logs/`
pages), turning the developer's play-test into the test CI can't run:
- Range rings: `[range-ring] built N ring(s) for the selected fleet (+ bubble vs #id)` — **0 rings when rings were
  expected names the wire, not the toggle, as the bug** — plus the bubble's computed reach.
- Nav maneuver: `[nav] phase-change committed: ship #N, K burns, Δv X of Y available` — proves the order fired
  with the cost the readout previewed, and that the Δv-budget number is sane on a real ship.

The rule (from the client CLAUDE.md): the engine accessor is CI's gauge; the `SessionLog` line is the *client's*
gauge. A client feature without a log line is a wire you can't see move.

## What's still open (the honest list)

- **Missile range** has no number at all yet (`MissileLauncherAtb.IsInRange` returns `true` always — v2 stub). Any
  missile ring would be a lie until that's built — so it deliberately isn't drawn.
- **Detection bubble vs the selected enemy — DONE** (`SensorTools.DetectionRangeAgainst`; cyan ring around the
  clicked enemy, reads the target's real signature so it shrinks when the enemy goes dark).
- **Maneuver fuel-cost preview — DONE** (NavWindow phase-change/phasing: est. fuel + Δv-used-of-available + red
  NOT ENOUGH Δv). The thrust/edit modes can take the same preview if wanted.
- **Sensor-ring radius is captured at build time**, so it doesn't live-update when a ship changes EMCON posture —
  re-toggle (or change selection) to refresh. A per-frame refresh is the follow-up if it's wanted.

## Finding: the base laser's two-zone falloff never fires (focal length 200× its range)

Surfaced while gauging the weapon-range readout. The two-zone beam model (Weapons CLAUDE Decision 1) is "full
energy inside `OptimalRange_m` (= focal length); inverse-square falloff from there out to `MaxRange`" — which
assumes **focal length < max range**. But the base-mod laser (`weapons.json`) ships **`Range` = 5,000 m** and
**`Focal Length` = 1,000,000 m** (a "Distance to target (debug)" property left at its placeholder), so
`OptimalRange_m` is 200× the gun's actual reach. `BeamWeaponProcessor.OnHit` only attenuates **beyond** optimal
(`if (distance > OptimalRange_m)`) and caps `energyScale` at 1.0 otherwise — so **no damage bug** (it never
amplifies) — but every reachable hit is at ≤ 5,000 m ≪ 1,000,000 m, so the falloff branch is **never taken**: the
laser deals flat full energy at every range it can fire. The feature is **dead for this design**. **Fix is data,
not code** (a balance call, flagged not changed): set `Focal Length` below `Range` (e.g. `Range * 0.5`) so a real
falloff band exists — or declare "flat damage to max range" the intent and label it so.

---

## The rule this leaves behind

When a system "doesn't communicate," **ask first: does the number EXIST?** If yes → it's a wire (cheap, do it). If
no → building the number is the task, and it's an engine job before a UI job. Don't draw a dial for a reading nobody
took; and don't assume a missing readout means a missing *number* — half the time the gauge is built and just
unwired (Failure A was the majority here).
