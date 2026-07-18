# Ground Tactical AI — the Posture Brain (Earthfall G2.2)

**Status: 🔒 design (2026-07-17). The answer to the developer's question: "is the AI smart enough to engage in ground combat and know when to be defensive, offensive, etc." — today NO; this doc is the plan that makes it YES, with every interrelated system it depends on named and placed in a campaign lane.**

---

## 1. The question, and today's honest answer

The developer asked whether the AI knows *when* to be defensive versus offensive on the ground. Verified answer (greps run 2026-07-17, this session):

**The LEVERS exist and have real teeth:**
- **Stance** — `GroundFormationDoctrine.TrySetStance` (GroundFormationDoctrine.cs:43): Offensive (+25% dealt / +25% taken), Defensive/Dig-In (−25%/−25%), Balanced — a moddable catalog (`groundStances.json`) with a switch cooldown, read per-unit by the combat resolver.
- **Rules of engagement** — `SetEngagementStance` (:61): Hold Ground / Close-to-Engage / Stand-Off, consumed by the auto-kite maneuver (`GroundForcesProcessor.ApplyEngagementManeuvers`) that steps units toward or away from the enemy to exploit weapon range.
- **The reserve doctrine** — `GroundReinforcement.WouldStripReserve` + `MilitaryCommand.GroundReserveFactor` (aggressive factions keep smaller reserves).
- **The battlefield reads** — fortification (`GroundFortification.DefenseMult`), terrain (`GroundTerrain`), formation aggregates (`FormationStrength/Health/ReachHexes/SpeedMult`).

**The BRAIN is absent:**
- **Zero AI code sets a stance or ROE.** `Factions/` contains no caller of `TrySetStance`/`SetEngagementStance` — the levers are player-only.
- **The AI never forms battalions** (`CreateFormation`'s only non-test caller is the player's PlanetView panel), so the levers *can't even apply* to AI units — stance/ROE live on a formation, and AI units field with `FormationId = -1`. The auto-kite never fires for them.
- There is no ground fight-or-flee, no dig-in-when-outnumbered, no retreat.

**The proof it's buildable — the space AI already does exactly this, and we mirror it, not invent:**
- `CombatRisk.WouldEngage` (CombatRisk.cs:39 pure / :50 entity): commit only when own strength ≥ enemy × a **Risk-trait curve** (bold = parity 1.0×, cautious = 2.0×, neutral 1.5×).
- `NPCDecisionProcessor.RunFleetDoctrinePolicy` (:721): a bold/aggressive personality runs Offensive + Weapons-Free; a cautious one runs Defensive + Return-Fire.
- `FleetRetreat`: fighting-withdrawal posture + a losses threshold that breaks a fleet off.
- Aurora's spec agrees on the shape: `docs/aurora/GROUND-COMBAT.md §4a` — utility scoring over observable state ("score stay / pull-back / commit-reserve / retreat"); RL/LLM approaches explicitly ruled out.

> Shipboard analogy: the ground force today is a gun crew with a working mount, working fire-control switches, and **no officer of the deck**. Everything below is the officer: read the plot, weigh the odds, pick the posture, give the helm order.

---

## 2. The decision model (the brain itself)

One pure, deterministic function — no randomness, no wall clock, unit-testable in isolation:

```
GroundTactics.DecidePosture(ctx) → { Stance, Roe, Intent, Reason }
   Stance ∈ { Offensive, Defensive, Balanced }          → applied via TrySetStance
   Roe    ∈ { HoldGround, CloseToEngage, StandOff }     → applied via SetEngagementStance
   Intent ∈ { Advance, Hold, PullBack, Retreat }        → applied via the order queue (MoveRegion)
   Reason : one plain-English line, recorded (the AI-tape rule: no decision without its explain)
```

**Inputs (`ctx`) — every one an existing or named read:**

| Input | Source | Status |
|---|---|---|
| Own battalion strength | `GroundFormationTools.FormationStrength/Health` | ✅ built |
| **Detected** enemy strength, this + adjacent regions | **NEW `GroundThreat` read over the per-faction ground fog** (fog slices 1–4 built; the fog-limited garrison read is the open "slice 5") | 🔴 the load-bearing new piece |
| Personality: Aggression + Risk | `PersonalityDB` via the **same** `CombatRisk.RequiredStrengthRatio` curve the fleets use | ✅ built (read-only reference) |
| Role: attacker vs homeland defender | derived: is this body home to my own colony? | trivial derive |
| Orbital support | own warships hold the body's orbit (the `HasOrbitalControl` presence read, inverted) | ✅ built |
| Fortification + terrain of my region | `GroundFortification.DefenseMult`, `GroundTerrain` | ✅ built |
| Reserve state | `GroundReinforcement` + `MilitaryCommand.GroundReserveFactor` | ✅ built |
| Ammo fraction | `GroundAmmo.Fraction` (real once G2.3 wires the drain) | ✅ pool built, drain in G2.3 |
| Somewhere to fall back to | nearest friendly-held region / G1 beachhead anchor | needs G1 |

**The rules (all thresholds FLAGGED balance values — the developer sets the numbers):**

| Situation | Decision | Why |
|---|---|---|
| Attacker, odds ≥ required-by-Risk (the CombatRisk curve) | **Offensive + Close + Advance** | you have the edge your personality demands — press it |
| Outnumbered, or defender on a fortified line | **Defensive/Dig-In + Hold** | the +fortification × −25%-taken stack is the force multiplier |
| Losing hard (own ≪ enemy) with a friendly region/beachhead behind | **Retreat** toward it | the ground echo of the fleet's fighting withdrawal |
| Losing hard, nowhere to go | **Defensive + Hold** | cornered units fight; no suicide marches, no teleports |
| Roughly even | **Balanced + StandOff** | probe with the range advantage (the kite already built) |
| Blind (no scouted read on the enemy) | bias cautious — treat unknown as risk | fog honesty: what you can't see can hurt you |
| Dry ammo | never Offensive | a silent gun line doesn't charge |
| Defender on own fortified homeland, even at parity | bias Defensive | home turf + prepared positions |
| Attacker with orbital support overhead | bias Offensive | the soften-then-advance sequence (bombard rung, PW) |

**Posture stability (the P3.3 lesson applied at design time):** decisions re-evaluate on the ground tick but a posture holds until the odds cross a band (hysteresis), respecting the stance cooldown — AND the hold releases immediately when the situation flips materially (the triggering condition clears). We build the break-glass in from day one; no 180-day locks here.

---

## 3. The interrelated systems — what must exist for the brain to work

This is the "extensively consider the implications" section: the full dependency web, each piece placed in its campaign lane.

1. **Battalions first (G2.1 — hard prerequisite).** Stance/ROE apply to a *formation*. `FormUpLoose` must form AI units at garrison-raise and at landing, or the brain has no hands. Already in the plan; now explicitly ordered before G2.2.
2. **The fog-honest threat read (`GroundThreat`) — the load-bearing new build.** The brain must read *detected* enemy strength, not omniscient truth — an undetected garrison must not be counted (matching `CombatRisk`'s undetected-clears rule in space). This IS the open "fog slice 5" (fog-limited enemy garrison read) from the surface-fog design — promoted from a someday-item to a required sub-slice. It also feeds the **easiest-landing score** (PW's landing-region choice) — one read, two consumers, built once in the GROUND lane. Implication: **scouting matters** — a ground radar/scout genuinely changes the AI's confidence, closing the recon→decision loop cradle-to-grave.
3. **Personality consistency across domains.** The same Aggression/Risk traits and the same required-ratio curve the fleet AI uses — so the UMF is *recognizably the UMF* on the ground and in space. Read-only reference to CORE-owned types (reads cross the lane fence freely; edits never do).
4. **Retreat needs a destination.** Retreat = MoveRegion toward the nearest friendly region or the **G1 beachhead** — which makes the beachhead a real *rally point*, not scenery. A force that loses its beachhead loses its line of retreat: infrastructure with stakes.
5. **Order ownership (a real gap this design surfaced).** The brain acts only on battalions with no queued *player* orders — the human always overrides. But today a `GroundOrder` doesn't record who issued it. Requirement: a small save-safe marker (issuer flag on the order or an "AI-controlled" bit on the formation) so brain-issued orders can be replaced by the brain while player orders are sacred. `[JsonProperty]` + deep-copy discipline.
6. **Sustainment feedback loop (G2.3).** Ammo drain → `Fraction` input → dry units go defensive → resupply on friendly ground / at the beachhead → confidence returns. The brain is what makes the logistics slice *felt*.
7. **Infrastructure combat interface (G3).** v1 split: the brain owns *posture and movement*; the resolver rung (PW) owns *tasking* (queue Destroy/Capture-infrastructure on an Offensive battalion in reach). The brain's Stance is the gate the tasking respects — a dug-in defender doesn't get siege tasking.
8. **Strategy → tactics seam (mission-command).** The strategic layer (Conquer/Defend resolvers, CORE) sets the campaign role — *this planet is an invasion / this planet is home defense*. v1 derives role from colony ownership; the seam is designed so a later explicit MANDATE (the mission-command two-slot protocol) and the **Battalion Commander seat** (governance roster #13) can plug in: a seated officer's competence will modulate the thresholds at one entry point (`DecidePosture`), exactly how `MilitaryCommand.PostureFor` is the fleet-reserve seam. No People/seat code now — the seam only.
9. **Visibility (the Visibility Gate).** Every decision returns a `Reason` recorded like the faction AI tape — and the CLIENT lane's Force Management window shows own battalions' stance/ROE/intent + reason. Fog rule: you see an **enemy** battalion's posture only through its observed behavior, never its reasoning — no intel leak. (Cross-lane request to CLIENT, logged in lane notes.)
10. **2D group-plane compatibility (TWOD lanes).** No conflict by construction: the brain decides *posture* (Stance/ROE/Intent); the future ground-on-plane slice (S4) resolves *geometry* (role standoffs/bearings). DecidePosture's outputs are exactly the doctrine inputs the plane's role-geometry table consumes. The interface holds across both worlds.
11. **Determinism.** Pure function, id tiebreaks, no RNG, decisions on the fixed hourly ground tick → fast-forward == watch, CI-testable, replay-stable.
12. **Byte-identity.** Everything behind `EnableGroundTacticalAI` (default OFF; menu/DevTest games flip it in PW alongside the other AI gates). Flag off = today's behavior, bit for bit.

---

## 4. Build spec (replaces the old "auto-advance" G2.2 — which was a mindless zerg-rush and is retired)

- **G2.2a — `GroundThreat`** (the fog-limited enemy strength read, this + adjacent regions; the fog "slice 5"). Pure, fog-honest, deterministic; also consumed by PW's landing score. Tests: scouted enemy counted; unscouted enemy invisible; reveal changes the read.
- **G2.2b — `GroundTactics.DecidePosture`** (pure): the §2 model + the order-ownership marker. Tests: the six acceptance gauges below, direct-call, no sim advance.
- **G2.2c — the wire**: a step in `GroundForcesProcessor`'s existing tick (L9 — no new hotloop) behind `EnableGroundTacticalAI`; applies Stance/ROE via the real setters, Intent via the real order queue; player orders always win; posture hysteresis + cooldown respected; Reason recorded.

**Acceptance gauges (the tests that answer the developer's question):**
1. An outnumbered defender on a fortified region picks **Defensive + Hold** and does not advance.
2. An attacker with a 2:1 edge picks **Offensive + Close + Advance** and takes a small multi-region world end-to-end (the campaign's missing capture test).
3. A battalion losing 1:4 with friendly ground behind **Retreats** toward it; cornered, it digs in instead.
4. At parity: **Balanced + StandOff** (the kite works for the AI).
5. **Personality bites**: a bold faction commits at odds a cautious one refuses — same curve as the fleets.
6. Flag off ⇒ byte-identical (the tripwire).
- P8.1 (the Earthfall acceptance) gains posture assertions — including the developer's declared scenario: **when the player's Space Marines counterattack in force, the UMF invaders read the odds and go defensive / withdraw to their beachhead** — the moment the brain is visibly alive.

## 5. Developer decisions & flagged numbers queued by this design

Odds thresholds per posture (reuse the CombatRisk curve — endpoints FLAGGED) · retreat trigger ratio + losses threshold · posture hysteresis band + minimum hold · blind-caution factor (how much an unscouted region scares the AI) · dry-ammo behavior detail · whether an enemy battalion's stance is ever directly readable at high recon levels (v2 intel question).
