# Operation Earthfall — Developer Decisions (answered 2026-07-21)

The developer answered the campaign's queued decisions after all four lanes merged
(GROUND → DEV → CLIENT → TWOD, all CI-green), authorizing an autonomous overnight run
of PW + P8. This file is the record; the FLAGGED balance dials stay at their defaults
(the developer will tune them from live game data — "a tear or two" of play-testing).

## Build-shaping decisions (applied)

1. **Combined fleet + battalion window name = "Force Management."** (CLIENT C3.)

2. **The Space Marine is the developer's live in-game test, NOT a CI fixture.** The
   developer will design the sealed + veteran-cadre + power-armor marine unit *in the
   game* as their own end-to-end test of the unit-creation system. So **P8 does not
   hard-code a Space Marine.** P8.1 = the invasion acceptance gauge (chain milestones)
   + making sure the player unit-creation → battalion → defend/invade **rails** are
   green and generic, so the developer's live build rides working rails. The
   Space-Marine-defends-then-invades acceptance is the developer's live test.

3. **All built ships boot charged + fuelled — player ships too.** `ShipDesign.
   ChargeBuiltPlayerShips` flipped `false → true` (was the deliberate "player earns
   charge over game-time" default). Frictionless: anything you build is ready to fly.
   One-line revert if the developer wants the earn-it mechanic back. (CI unaffected —
   see `ShipDesign.cs` ProvisionBuiltShip doc.)

4. **Orbital bombardment re-fire is TABLED.** PW does **not** build the bombardment
   re-fire cadence / rung. The existing one-shot orbital bombardment (softens a
   defending garrison — `GroundBombardmentTests`) stays as-is.

5. **Regions are cosmetic; the hex is the unit of everything.** *"regions mean nothing,
   they're a visual aid. it's all about hexes."* So the invasion is wired at the **hex**
   level — landing, infrastructure destroy/capture, and ownership all resolve per-hex
   (GROUND's `GroundHex` + per-hex capture are the real mechanism). **No region-transfer
   logic.** Any PW slice phrased in terms of "region" is redirected to "hex."

## Balance dials — defaults stand (developer tunes from live play)

rebellion-debounce **2** · crisis-dwell **60 days** · contradiction-release **14** ·
Mars launch fuel **20M methalox + 20M ntp** · station tax **0.15** · battalion cap **6** ·
ammo/salvo **1.0** · upkeep/mass **0.1** · the six ground-tactical-brain thresholds
(dry-ammo 0.05, retreat-loss ratio, posture hysteresis band, minimum hold, blind-caution
factor, dry-ammo behavior) · ground cadre talent-draw size · 200 Gm homeworld sensor
horizon · launch-pad tonnage **100,000 kg**.

## Still deferred (not this run)

Orbital strike aimed at a specific hex's buildings (the developer deferred this earlier —
the W-track follow-on). Command-org 2-tier → 4-tier expansion (task #22).
