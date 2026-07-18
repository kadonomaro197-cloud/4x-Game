# The 200 Gm Homeworld Sensor Horizon — a design call for the developer

**Status: DECISION MEMO (Operation Earthfall C2.1). No engine value is changed by this slice.** This memo lays out
the choice and its knobs; the developer picks. The C2.1 code only made the *readouts* honest (the map ring and the
`[DETECT]` log line) — the sim number is untouched.

---

## The one-paragraph version

Your homeworld colony (Earth) carries a **megasensor**: a deliberately huge, 200 billion-meter (200 Gm) detection
horizon, set on purpose 2026-07-14 so the capital acts as **system-wide early warning** — it spots fleets creeping in
at Mercury, Venus, and Mars long before they arrive. That was working exactly as designed. What went wrong is the
*picture*: the green "detection range" ring drawn around Earth was sized against **one arbitrary reference ship**, so a
**louder** enemy fleet was genuinely detected while drawn **outside** the ring — you "saw them before sensor range,"
and it read like a bug. It wasn't. C2.1 fixed the picture (see below). The remaining question is whether you *keep*
the 200 Gm early-warning bubble as-is, or make it smarter. **You don't have to change it — "keep it" is a valid,
recommended default now that the readouts are honest.**

---

## What C2.1 already fixed (no value change, shipped this slice)

Think of it like a radar repeater that was drawing the wrong range ring on the scope — the radar was fine, the grease-
pencil circle was wrong.

1. **Honest detection band on the map.** The colony's green ring is now sized against the **actual contacts present**,
   not one reference ship. It draws a **band**: an **outer** ring against the *loudest* ship out there (so a detected
   contact can **never** render outside it) and an **inner** ring against the *quietest* (detection reach genuinely
   *varies* with how loud the target runs — the band shows that). Both are still clamped to the true 200 Gm horizon,
   so the ring never promises reach the sensor doesn't have. Dial: `Pulsar4X.Client/Rendering/SystemMapRendering.cs`,
   `UpdateAllRangeRings` colony loop.
2. **Held-vs-fresh split in the `[DETECT]` log** (behind a default-off switch, `SessionLog.SplitHeldVsFresh`). Turn it
   on and the log tells apart contacts you're **tracking right now** (fresh) from contacts you're **coasting on a
   last-known fix** (held-stale). This surfaces a real quirk without changing gameplay: the combat "is this hostile
   detected?" gate counts a *stale* contact as detected too, so a raw "holds N" over-reports what you're actually
   watching this instant.

Neither touched the sim. The engine still detects exactly what it detected before.

---

## The design call: what to do with the 200 Gm horizon itself

The horizon is **one literal** in the data. It is a **hard, signature-independent cap**: beyond 200 Gm the colony
detects **nothing**, however loud the target; inside it, normal signal-vs-threshold rules apply.

- **The dial (the literal):** `Pulsar4X/GameData/basemod/TemplateFiles/electronics.json:175` — the 7th argument to
  `AtbConstrArgs(...)`, currently `200000000000` (200 Gm). This becomes `SensorReceiverAtb.MaxDetectionRange_m`.
- **Where the cap bites (the scan):** `GameEngine/Sensors/SensorRecever/SensorTools.cs`, `GetDetectedEntites` (the
  `if (sensorAtb.MaxDetectionRange_m > 0 && distance > sensorAtb.MaxDetectionRange_m) continue;` line) — and the
  readout clamp in `DetectionRange_m` (same file) keeps the ring matching the scan.
- **Ship sensors are unaffected** either way: a warship's passive sensor uses the same template but its real reach is
  ~0.3 Gm, far *inside* 200 Gm, so the horizon never bites on a ship — combat detection is byte-identical.

### Option 1 — KEEP + honest readouts *(the default; already shipped, no engine change)*

Leave the horizon at 200 Gm. The capital stays a system-wide early-warning post; the honest ring + the `[DETECT]`
split (above) remove the confusion that made it *look* wrong.

- **Gameplay consequence:** exactly today's feel — you get strategic warning of anything approaching the inner system,
  and the map no longer lies about it. No stealth counter at strategic range (a fleet running dark is still seen at
  200 Gm if it's within the horizon and above threshold).
- **Dial to touch:** none. (`electronics.json:175` stays `200000000000`.)
- **Recommendation:** this is the right default. Ship it, play with it, and only move to Option 2/3 if you decide
  strategic-range **stealth** should be a real decision.

### Option 2 — SIGNATURE-CURVE the horizon *(engine change — a real behavior shift)*

Make the horizon depend on how loud the target is instead of a flat cap: a **loud** warship is seen out to ~200 Gm, a
**quiet/coasting** one only much closer. Today reach *inside* the horizon already scales with signature (`√signal`);
this option removes the *hard* cap and lets the natural signature falloff be the only limit (or scales the cap by the
target's activity multiplier).

- **Gameplay consequence:** **EMCON matters at strategic range.** A fleet that goes dark (Silent posture) can slip
  *inside* the early-warning bubble undetected and only light up when it burns or fires near the inner planets — the
  "dark-vs-loud" decision (already a thing in close combat) now reaches all the way out to how you *approach* a
  defended homeworld. Costs the player their guaranteed 200 Gm warning against a careful attacker.
- **Dial to touch:** `SensorTools.cs` `GetDetectedEntites` — replace the flat `MaxDetectionRange_m` compare with a
  signature-scaled cap (and mirror it in `DetectionRange_m` so the ring stays honest). Bigger change; needs its own
  test.

### Option 3 — EMCON-GATE the horizon *(engine change — keep the outer wall, let stealth beat it)*

Keep the 200 Gm horizon as the **outer wall** (nothing is ever seen past it), but let a fleet on **Silent** drop below
the colony's detection *threshold* at long range even while inside the wall — so the horizon is the ceiling, but EMCON
decides whether you're actually seen underneath it.

- **Gameplay consequence:** the middle path. You can never be surprised from *beyond* 200 Gm (the wall holds), but a
  disciplined attacker running dark can close a long way inside it before the homeworld picks them up — a standing
  reason to invest in EMCON on an approaching fleet, and in better colony sensors on defense. Less of a nerf to
  early-warning than Option 2 (the wall still guarantees *something*), more of a lever than Option 1.
- **Dial to touch:** the horizon literal stays `electronics.json:175`; the added behavior reads the target's
  `SensorProfileDB.ActivityMultiplier` (the EMCON dial) against the colony's threshold at range in `SensorTools.cs`.

---

## Recommendation

**Ship Option 1 now** (it's already done, zero risk, and the honest readouts remove the actual confusion). Revisit
Option 2 or 3 later, *only if* you decide strategic-range **stealth** should be a player decision — that's a genuine
gameplay addition, not a bug fix, and it wants its own slice + test. The tension to weigh: the 200 Gm bubble was set
deliberately as early warning; Options 2/3 trade some of that guaranteed warning for a stealth counter.

---

*Cross-references: `docs/combat/DETECTION-DESIGN.md` (the dark-vs-loud EMCON design), `GameEngine/Sensors/CLAUDE.md`
("Colony sensors have a HARD detection HORIZON"), `docs/earthfall/findings/A2-ghost-contacts.md` (the diagnosis).*
