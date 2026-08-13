# Litmus — Building a Venator-class Star Destroyer (the closest the ship designer gets, 2026-08-01)

> **The ask:** build a Venator-class Star Destroyer in the current ship designer and get as close as possible.
> The Venator is the ideal *ship* test because it is **three ships in one** — a turbolaser warship, a
> starfighter carrier, and a clone-army/walker transport — and one of those three lands squarely on the
> half-built seam the interconnection audit just flagged.
>
> **Headline:** as a **warship and a transport it gets further than the marine did as a soldier** — the ship
> combat model (weapon nature vs shields, armour matchups, evasion, battery-buffered power, warp) is the most
> complete system in the game, so a Venator *fights and jumps like a capital ship for real.* But its
> **signature identity — the fighter carrier — is only in potential**: the hangar bay is a genuine buildable
> component, but the order that launches a strike wing to fight as a sub-fleet isn't wired yet. A superb Star
> Destroyer warship-and-transport; a carrier on paper.

---

## The target

A **Venator-class Star Destroyer** (Clone Wars, Republic): ~1,137 m, DBY-827 heavy dual turbolaser batteries,
point-defence laser cannons, proton torpedoes, deflector shields, a hypermatter reactor and sublight ion
drives, a **class-5 hyperdrive**, a **dorsal hangar carrying ~420 starfighters**, and a belly full of **clone
troopers and AT-TE walkers** for planetary assault. All three roles below are real components you mount in the
Entity Assembler on one **heavy hull**.

---

## The build — real components on a heavy hull (Mass Budget 180,000 kg)

The Aegis Test Warship is the pattern (heavy hull + lasers + reactors + batteries + thrust + warp); the Venator
scales it up and adds the carrier and transport decks.

### Hull & armour
| Component | Count | Notes |
|-----------|-------|-------|
| **Ship Hull (Heavy)** | ×1 | the biggest frame — **Mass Budget 180,000 kg** (the cap the assembler enforces; the client turns enforcement on) |
| **Armor** (heavy plating) | thickness ~10 | the Aegis runs thickness 6; a Star Destroyer plates thicker (per-nature soak tuned vs turbolaser energy) |

### Turbolaser batteries + point defence + torpedoes (Weapons door)
| Component | Count | Stands in for |
|-----------|-------|---------------|
| **Long-Range Laser** | ×8 | the DBY-827 heavy dual turbolaser turrets (energy nature, long reach) |
| **Laser Weapon** | ×12 | medium turbolasers / broadside batteries |
| **Chain-Flak Battery** | ×8 | the point-defence laser cannons — **flak shreds saturation-heavy fighter swarms** (the built-in counter to a carrier's own wing) |
| **Missile launcher + Ammo Magazine** | ×4 | proton torpedo tubes (kinetic/explosive delivery, fed from a magazine) |

### Deflector shields (Defense door)
| Component | Count | Notes |
|-----------|-------|-------|
| **Shield Generator** | ×4 | the deflector screen — a depleting pool (capacity + regen) peeled before armour |

### The hypermatter reactor + battery buffer (Power door)
| Component | Count | Notes |
|-----------|-------|-------|
| **Fission Reactor** | ×6 | the power plant — turbolasers are huge burst loads |
| **Battery 2t** | ×8 | **buffers the turbolaser draw** — a beam fires from *stored* energy and the reactor recharges between salvos (the real battery-buffered model; it's why a warship carries batteries) |
| **Fuel tank** | ×4 | reactor fuel |

### Sublight ion drives + hyperdrive (Propulsion door)
| Component | Count | Notes |
|-----------|-------|-------|
| **High-Thrust Reactionless Drive** | ×6 | the sublight ion engines (thrust ÷ mass → the ship's evasion, bought here) |
| **Alcubierre Warp Drive** (2k) + **Warp Stabilizer** | ×1 + ×1 | the class-5 hyperdrive (departure gated by stored energy) |

### Sensors + gunnery (Sensors door)
| Component | Count | Notes |
|-----------|-------|-------|
| **Passive Sensor** + **Beam Fire Control** | ×1 + ×2 | detection + turbolaser targeting |

### THE HANGAR — the signature (Logistics ▸ Docking)
| Component | Count | Notes |
|-----------|-------|-------|
| **Docking Bay** | ×many | a real component — **`Berths` dial (4 whole ships each)**; stack them for the fighter complement. A docked ship re-parents to the carrier, so it travels with it |

### THE TROOP/WALKER DECK (Logistics ▸ Bay)
| Component | Count | Notes |
|-----------|-------|-------|
| **Troop Bay** (`GroundBayAtb`) | ×several | carries clone troopers + **AT-TE walkers** (vehicle carry-class) → the invasion chain (load → land) |

Everything above fits the 180,000 kg budget by trading counts (the assembler's live "OVER" readout tells you
when you've overpacked; enforcement is on in-game). The fighters, the AT-TEs, and the clone units are each
**their own small designs** built separately and loaded — real entities, not abstractions.

---

## The assembled Star Destroyer — by role

- **As a warship:** ✅ a genuine capital combatant. Turbolasers (energy) hammer shields; flak batteries shred
  incoming fighters; the deflector pool soaks before the armour matchup; the battery bank buffers the burst
  draw so the guns keep firing while the reactor recharges; thrust÷mass sets its (low) evasion; the hyperdrive
  jumps it out. The ship combat model reads *every* one of these — it's the most complete system in the game.
- **As a transport:** ✅ real. Clone battalions and AT-TE walkers ride the troop bays and **land on a planet**
  through the working invasion chain (load → sail → deploy).
- **As a carrier:** ⚠️ *in potential only.* You can **build the hangar** and **dock starfighter-ships** in it,
  and they travel with the Venator — but you **cannot launch a fighter screen that fights.** `DockTools` has
  no game caller yet ("the order is the next slice"), and strike-craft-flying-as-a-sub-fleet is the
  design-locked-but-unbuilt `CARRIER-DESIGN.md`. The Venator's defining trick is a bay full of fighters that
  can't yet scramble.

---

## Accuracy scorecard

### ✅ Captured (real, in the sim)
- **Turbolaser warfare** — energy weapons vs deflector shields vs armour, the full matchup triangle.
- **Point-defence vs fighters** — flak's anti-saturation role is exactly the counter a carrier needs.
- **Battery-buffered reactor** — the hypermatter-reactor-feeds-batteries-feed-guns model is how ship power
  actually works (unlike the marine, whose power was simpler).
- **Deflector shields** — a depleting pool with capacity + regen, peeled before armour.
- **Sublight + hyperdrive** — thrust, evasion, and a real FTL jump gated on stored power.
- **Clone/walker transport** — troop bays land an army; the invasion chain works.
- **Fighters as real ships** — each starfighter is a buildable small design, docked and carried.

### ⚠️ Approximated (there, but not the real thing)
- **The carrier launch** — the bay is real and holds fighters, but the *launch-and-fight-as-a-wing* loop is
  unbuilt. This is the single biggest gap for *this* ship, because the carrier is its identity.
- **Scale** — "Heavy hull, 180 t budget" is the largest frame, not a modelled 1,137 m. A Venator vs a corvette
  is how much you cram on the biggest hull, capped by the mass budget.
- **Elite Republic gunnery** — the firepower-caliber enhancer is **LIVE in real combat** *(corrected 2026-08-13;
  the earlier "dead / D-gate-2" note was stale, the ship fix landed ~2026-08-02)*: `ShipCombatValueDB.cs:529`
  applies `firepower *= UnitCaliberFirepowerMult`, and `Calculate` is called by both live resolvers
  (`AutoResolve.cs:124`, `CombatEngagement.cs:1656`) — so caliber **does** make the turbolasers hit harder.
- **Individual turrets** — weapons resolve as a bucketed fire mix by class, not eight named DBY-827 turrets
  (fine for auto-resolve; not a turret sim).

### ❌ / ⚠ Gaps worth knowing
- **No ship energy-weapon gate** — a Venator with turbolasers and *no reactor* would be "legal" in the ship
  designer (ships don't enforce the reactor requirement; only ground units do — audit finding). I built it
  correctly with reactors + batteries, but the gate that *should* stop a powerless gunship is missing.
- **No shield power cost** — the deflectors recharge free; holding shields doesn't tax the reactor (audit
  crack C15 / Design 1). A real Venator trades power between guns, shields, and engines; this one doesn't.

---

## Bottom line

**You can build a Venator today that is a convincing Star Destroyer from two of its three angles.** As a
turbolaser warship it is genuinely there — the ship combat model is the most finished system in the game, so
it fights, shields, jumps, and screens against fighters like a capital ship should, on a battery-buffered
reactor that behaves the way the fiction implies. As a clone-army-and-walker transport it is there too — the
troop bays land an army through a working invasion chain.

**Where it isn't a Venator is precisely its signature:** the fighter carrier. The hangar bay is a real
component and it holds real starfighter-ships, but the order that scrambles a wing to fight as a sub-fleet
isn't wired — so the most iconic image of the ship, its dorsal bay disgorging 400 fighters, is a deck full of
craft that can't yet launch. That is not a paint-over; it's the exact seam the audit found (the Docking Bay is
built, the launch order is "the next slice," and the carrier combat loop is design-locked but unbuilt).

**Closeness, honestly: ~75% as a warship + transport, ~45% as a carrier — call it ~65% of the whole ship.**
The capital warship is nearly complete; the carrier is a bay waiting for its launch order and its
strike-craft-as-sub-fleet resolver — both already designed (`CARRIER-DESIGN.md`), neither yet built. Finish
that one seam and the Venator jumps from a very good gunship-with-a-garage to an actual Star Destroyer.

---

*Built against `componentDesigns.json` (Ship Hull Heavy = 180,000 kg budget; Aegis Test Warship pattern) +
the ship assembler, 2026-08-01. Carrier gap: `docs/combat/CARRIER-DESIGN.md` (design-locked, unbuilt) +
`GameEngine/Docking/` (DockTools built, no game caller). Companion litmus: the Blood Angels marine build.*
