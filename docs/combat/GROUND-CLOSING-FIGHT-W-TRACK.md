# The W-Track — per-weapon range banding + formation-role parity for ground combat

**As of:** 2026-07-21 · branch `claude/devtest-faction-design-xpfnhe` · **status: PLAN (build W1 first, CI-gated)**

> **Why this exists.** The franchise litmus test (`docs/showcase/FRANCHISE-LITMUS-TEST.md`) and the developer's
> combat-fidelity call exposed the same gap from two directions: **a ground unit collapses all its weapons into ONE
> Attack + ONE Range**, so a Space Marine squad's lascannon, bolter, and chainsword all reach the enemy at the same
> instant — where a SHIP fires each weapon as its own range band opens during the close. This track closes that gap by
> finishing the resolver-merge North Star (`docs/combat/RESOLVER-DESIGN.md` §B7) for the **weapon-loadout** dimension.

---

## 1. The problem, in one picture

**Space (correct today).** A ship carries `List<WeaponProfile> Weapons` (`Combat/ShipCombatValueDB.cs:179`). As two
fleets close, `CombatEngagement` fires **each weapon only once its own `WeaponProfile.Range_m` reaches the shrinking
gap** (`CombatEngagement.cs:394`; the gap closes each step). A long beam opens up first; short flak waits until the
brawl. **Per-weapon range banding is real.**

**Ground (broken today).** `GroundUnitAssembly.Compute` **sums** every weapon into one `unit.Attack`, keeps only the
**longest** `unit.Range`, and stamps the flavour of the **heaviest hitter** (`GroundUnitAssembly.cs:137-139`).
`GroundCombatant.ToWeaponProfile` then makes **one** `WeaponProfile` from that single collapsed weapon
(`GroundCombatant.cs:66,104`), and `ResolveRegionCombat` gates fire on the unit's single `u.Range`
(`GroundForcesProcessor.cs:371`). So a squad's lascannon (long), bolter (medium), and chainsword (melee) are **one
blob fired at one band** — a slow Carnifex and a fast Hormagaunt "arrive" together, and no weapon staggers.

**The closing engine already exists** (RESOLVER-DESIGN §B7 slice 4, landed 2026-07-08): units carry hex positions,
`ApplyEngagementManeuvers` closes/kites one hex per tick per the formation's stance, `GroundMobility` makes a fast
unit close sooner, and a longer-ranged unit hits a closer one *without being hit back* (the first-strike). It just
runs on a **one-weapon-per-unit** model. **Give a unit a real multi-weapon loadout and the already-built closing
engine does the banding for free.** That is this track.

---

## 2. The slices (each additive, CI-green before the next — RESOLVER-DESIGN §B6 discipline)

### W1 — the multi-weapon ground loadout (data + assembler). ADDITIVE, byte-identical.

Give a `GroundUnit` a real per-weapon loadout instead of one collapsed number.

- **Data.** Add `GroundUnitDesign.WeaponLoadout` and `GroundUnit.WeaponLoadout` — a `List<GroundWeaponMount>` where a
  `GroundWeaponMount` is a small save-safe value object: `{ double Attack; int RangeHexes; GroundWeaponMode Mode;
  double Penetration; double PerShotEnergy; }`. `[JsonProperty]` + deep-copied in `Clone()` (the `GroundUnit` /
  `GroundUnitDesign` copy discipline — GroundCombat gotcha 2, save-safe from day one).
- **Assembler.** In `GroundUnitAssembly.Compute`, alongside the existing sum, append **one `GroundWeaponMount` per
  mounted `GroundWeaponAtb` component** (its own `Attack`, `Range`, `Mode`, `Penetration`, `PerShotEnergy`) — do NOT
  touch the atb ctor (gotcha 6: the JSON binder is exact-arity). Keep the existing `r.Attack` sum / `r.Range` max /
  `r.DamageType` top **unchanged** so the current resolver is byte-identical.
- **Snapshot.** `GroundForces.RaiseUnit` copies the design's `WeaponLoadout` onto the raised `GroundUnit` (like every
  other stat snapshot — GroundCombat gotcha 2).
- **Unwired.** `ResolveRegionCombat` still reads `u.Attack`/`u.Range` this slice → **live combat byte-identical**. The
  loadout list is populated but unread (the codebase's proven additive-then-wire pattern — cf. the kernel bridge
  3b-i).
- **Gauge (`Tests`).** A 2-weapon assembled unit (a long + a short ground weapon) has **two** `WeaponLoadout` entries
  with the right per-weapon ranges/attacks, and the summed `Attack` still equals the old collapse (byte-identity
  tripwire). A single-weapon unit has one entry. A garrison/DevTools/old-save unit (no loadout) is unaffected.

### W2 — the resolver fires per-weapon by range. BEHAVIOUR CHANGE (byte-identical for single-weapon units).

Wire the loadout into the closing fight.

- **Combatant view.** `GroundCombatant.ToCombatant` presents **one `WeaponProfile` per `WeaponLoadout` entry** (reuse
  the existing `ToWeaponProfile` mapping per mount). A unit with an empty loadout falls back to the single collapsed
  profile → byte-identical for garrison/old units.
- **Resolver.** `ResolveRegionCombat` builds the attacker's fire-mix from the **loadout** and gates **each** weapon on
  its own `RangeHexes` vs the current `HexDist` to the target (the space model, `BuildFireMix` range gate). Now the
  lascannon whittles at 5 hexes, the bolter opens at 2, the chainsword at contact — staggered by distance and the
  unit's move speed as it closes. Ammo/power draw already reads per-weapon (`WeaponSupply`), so a dry weapon silences
  without silencing the whole unit.
- **Gauge.** A squad with a long + short weapon, 4 hexes from a rusher, plays out over many ticks: only the long
  weapon lands during the approach, the short weapon adds its damage once the gap closes (the stagger, proven
  end-to-end — the ground twin of the space closing-fire test). A single-weapon unit reproduces
  `ClosingFight_LongRangeWhittlesTheRusherDuringTheApproach` exactly (byte-identity).

### W3 — sub-formation ROLE parity (the `Fleets.FleetRoleComposer` ground twin).

Give a battalion the sub-fleet-grade role depth: sort its formations into **Screen / Line / Artillery / Support** by
their loadout (long-range → Artillery, fast+evasive → Screen, heavy+armoured → Line), each with a role-doctrine that
drives closing — the artillery formation kites and fires, the screen rushes, the line holds — so **in one battle
different formations move at different speeds and engage at different ranges**, exactly like sub-fleets. Extends
`ApplyEngagementManeuvers` to read a formation's role (the Battalion▸Formation tree, per-formation stance, and
`GroundFormationTools` aggregation already exist — slice 5a). This is the "which unit is positioned where + its
doctrine" half of the developer's ask.

### W4 — bucket the ground resolver (perf, for the horde). Correctness-neutral.

The `O(units²)→O(buckets)` rewrite so a 5,000-gaunt swarm resolves in milliseconds. **The build plan already exists**
— RESOLVER-DESIGN §B7.2 (bucket key adds hex position; equivalence-proof against the per-unit resolve). Lands after
W1–W3; the loadout becomes part of the bucket key.

### W1b (follow-on) — space weapons compose on a ground unit (the buildability #1 fix + a calibration decision).

Today a ship-grade weapon (a `GenericBeamWeaponAtb` — an SPHA-T beam) mounted on a walker **draws reactor power but
contributes ZERO ground Attack** (`GroundUnitAssembly.Compute` reads only `GroundWeaponAtb`; `WeaponSupply.PowerDraw_W`
charges only the space atbs) — the litmus test's verified #1 gap. W1b extends the W1 loadout builder to also emit a
`GroundWeaponMount` from each mounted **space** weapon atb, deriving its ground Attack from its `WeaponProfile` output.
That derivation is a **calibration decision** (space damage is in joules; ground Attack is points) the developer
should set — so it's split out as its own slice, not blocking the closing-fight core (W1+W2), which needs only the
`GroundWeaponAtb` weapons a unit already carries.

---

## 3. Invariants (RESOLVER-DESIGN §B6)

- **Byte-identity for ships** — this track never touches the ship path; ship fixtures stay the tripwire.
- **Byte-identity for existing ground units** — a unit with one weapon (or no loadout list: garrison, DevTools,
  old saves) resolves exactly as today. W1 is additive/unread; W2's fallback covers the loadout-less case.
- **Save-safe** — every new field is `[JsonProperty]` + deep-copied (GroundCombat gotcha 2); `MidCampaignSaveLoadTests`
  and `SaveLoadDesignRoundTripTests` are the sensors.
- **Determinism** — the kernel stays pure/RNG-free (fast-forward == watch).
- **One slice per push, both CI jobs green before the next** — no stacking (CI is the only gauge; no local SDK).
- **No atb ctor changes** — the loadout is built at the assembler, stored on the design/unit (gotcha 6).

---

## 4. Order of build

**W1 → (gate) → W2 → (gate) → W3 → (gate) → W4.** W1+W2 are the heart of the developer's ask (per-weapon range
banding as units close) and land the closing-fight fidelity. W3 adds the sub-formation role depth. W4 makes the horde
cheap. W1b (space-weapon composition) folds in when the developer sets the space→ground damage calibration.

Cross-refs: `docs/combat/RESOLVER-DESIGN.md` §B7 (the North Star + the merge slices this completes),
`docs/showcase/FRANCHISE-LITMUS-TEST.md` (the litmus reading that surfaced the #1 gap),
`Pulsar4X/GameEngine/GroundCombat/CLAUDE.md` (the ground-combat file map + the closing engine as-built).
